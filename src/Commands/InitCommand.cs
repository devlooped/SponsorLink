using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace Devlooped.Sponsors;

[Description("Initializes a sponsorable manifest and token")]
public partial class InitCommand: AsyncCommand<InitCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("The base URL of the manifest issuer web app.")]
        [CommandOption("-i|--issuer")]
        public required string Issuer { get; init; }

        [Description("The intended audience or supported sponsorship platforms, e.g. https://github.com/sponsors/curl.")]
        [CommandOption("-a|--audience <VALUES>")]
        public required Uri[] Audience { get; init; }

        [Description("The Client ID of the GitHub OAuth application created by the sponsorable account.")]
        [CommandOption("-c|--clientId")]
        public required string ClientId { get; init; }

        [Description("Existing private key to use. By default, creates a new one.")]
        [CommandOption("-k|--key")]
        public required string? Key { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Issuer))
                return ValidationResult.Error("Issuer URL is required.");

            // must provide at least one audience
            if (Audience.Length == 0)
                return ValidationResult.Error("At least one audience is required.");

            if (Audience.Any(x => !x.IsAbsoluteUri))
                return ValidationResult.Error("Audiences must be absolute URIs.");

            if (!Audience.Any(x => x.Host == "github.com"))
                return ValidationResult.Error("At least one of the intended audiences must be a GitHub sponsors URL.");

            if (string.IsNullOrWhiteSpace(ClientId))
                return ValidationResult.Error("Client ID is required.");

            return base.Validate();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var sponsorable = settings.Audience
            .Where(x => x.Host == "github.com")
            .First().Segments[^1];

        // Generate key pair
        var rsa = RSA.Create(3072);
        if (settings.Key is not null)
        {
            if (!File.Exists(settings.Key))
            {
                AnsiConsole.MarkupLine($":cross_mark: Key file '{settings.Key}' does not exist.");
                return -1;
            }

            rsa.ImportRSAPrivateKey(await File.ReadAllBytesAsync(settings.Key), out _);
        }
        else
        {
            AnsiConsole.MarkupLine($":check_mark_button: Generated new signing key");
        }

        var pub64 = Convert.ToBase64String(rsa.ExportRSAPublicKey());
        var pubJwk = JsonSerializer.Serialize(
            JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false))),
            JsonOptions.JsonWebKey);

        var baseName = Path.Combine(Directory.GetCurrentDirectory(), sponsorable);

        // Only re-export if we generated a new key
        if (settings.Key == null)
        {
            await File.WriteAllBytesAsync($"{sponsorable}.key", rsa.ExportRSAPrivateKey());
            AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key[/]     [grey](private key)[/]");

            await File.WriteAllTextAsync($"{sponsorable}.key.txt",
                Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
                Encoding.UTF8);
            AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key.txt[/] [grey](base64-encoded)[/]");

            await File.WriteAllTextAsync($"{sponsorable}.key.jwk",
                JsonSerializer.Serialize(
                    JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(true))),
                    JsonOptions.JsonWebKey),
                Encoding.UTF8);
            AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key.jwk[/] [grey](JWK string)[/]");

            await File.WriteAllBytesAsync($"{sponsorable}.pub", rsa.ExportRSAPublicKey());
            AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub[/]     [grey](public key)[/]");

            await File.WriteAllTextAsync($"{sponsorable}.pub.txt", pub64, Encoding.UTF8);
            AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub.txt[/] [grey](base64-encoded)[/]");

            await File.WriteAllTextAsync($"{sponsorable}.pub.jwk",
                pubJwk,
                Encoding.UTF8);
            AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub.jwk[/] [grey](JWK string)[/]");
        }

        var manifest = new SponsorableManifest(new Uri(settings.Issuer), 
            settings.Audience, 
            settings.ClientId, new RsaSecurityKey(rsa), pub64);

        // Serialize the token and return as a string
        var jwt = manifest.ToJwt();

        var path = new FileInfo("sponsorable.jwt");
        await File.WriteAllTextAsync(path.FullName, jwt, Encoding.UTF8);

        AnsiConsole.MarkupLine($":check_mark_button: Generated new sponsorable JWT");
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{path.FullName}[/] [grey](upload to .github repo)[/]");
#if DEBUG
        AnsiConsole.MarkupLine($"\t:magnifying_glass_tilted_right: [grey]{jwt}[/]");
#endif

        // Showcases how use the JWK to perform validation (in this case of the sponsorable manifest itself).
        var jwk = JsonWebKey.Create(pubJwk);
        var secKey = new JsonWebKeySet { Keys = { jwk } }.GetSigningKeys().First();

        // If signature is valid, this will return a principal with the claims, otherwise, it would throw.
        var jwtPrincipal = new JwtSecurityTokenHandler().ValidateToken(jwt, new TokenValidationParameters
        {
            RequireExpirationTime = false,
            // The audiences must match each of the intended audiences
            AudienceValidator = (audiences, token, parameters) => audiences.All(audience => settings.Audience.Any(uri => uri.AbsoluteUri == audience)),
            ValidIssuer = manifest.Issuer,
            IssuerSigningKey = secKey,
        }, out var secToken);

        if (secToken is JwtSecurityToken jwtToken)
        {
            AnsiConsole.Write(new Panel(new JsonText(jwtToken.Header.SerializeToJson()))
            {
                Header = new PanelHeader("| JWT Header |"),
            });
            AnsiConsole.Write(new Panel(new JsonText(jwtToken.Payload.SerializeToJson()))
            {
                Header = new PanelHeader("| JWT Payload |"),
            });
        }

        return 0;
    }
}
