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
public partial class InitCommand(ICommandApp app) : GitHubAsyncCommand<InitCommand.Settings>(app)
{
    public class Settings : CommandSettings
    {
        [Description("The base URL of the manifest issuer web app.")]
        [CommandArgument(0, "<issuer>")]
        public required string Issuer { get; init; }

        [Description("The Client ID of the GitHub OAuth application created by the sponsorable account.")]
        [CommandArgument(1, "<clientId>")]
        public required string ClientId { get; init; }

        [Description("Sponsorable account, if different from the authenticated user.")]
        [CommandOption("-a|--account")]
        public required string? Account { get; init; }

        [Description("Existing private key to use. By default, creates a new one.")]
        [CommandOption("-k|--key")]
        public required string? Key { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var audience = settings.Account;

        if (audience == null) 
        {
            if (!AnsiConsole.Confirm(ThisAssembly.Strings.Init.NoAudience))
                return -1;

            if (await base.ExecuteAsync(context, settings) is var result &&
                result < 0)
                return result;
            else 
                audience = Account?.Login;
        }

        if (audience == null)
        {
            AnsiConsole.MarkupLine($":cross_mark: Could not determine sponsorable account to use for manifest.");
            return -1;
        }

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

        var pub64 = Convert.ToBase64String(rsa.ExportRSAPublicKey());
        var pubJwk = JsonSerializer.Serialize(
            JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false))),
            JsonOptions.JsonWebKey);

        AnsiConsole.MarkupLine($":check_mark_button: Generated new signing key");

        var baseName = Path.Combine(Directory.GetCurrentDirectory(), audience);

        await File.WriteAllBytesAsync($"{audience}.key", rsa.ExportRSAPrivateKey());
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key[/]     [grey](private key)[/]");

        await File.WriteAllTextAsync($"{audience}.key.txt",
            Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
            Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key.txt[/] [grey](base64-encoded)[/]");

        await File.WriteAllTextAsync($"{audience}.key.jwk",
            JsonSerializer.Serialize(
                JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(true))),
                JsonOptions.JsonWebKey),
            Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key.jwk[/] [grey](JWK string)[/]");

        await File.WriteAllBytesAsync($"{audience}.pub", rsa.ExportRSAPublicKey());
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub[/]     [grey](public key)[/]");

        await File.WriteAllTextAsync($"{audience}.pub.txt", pub64, Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub.txt[/] [grey](base64-encoded)[/]");

        await File.WriteAllTextAsync($"{audience}.pub.jwk",
            pubJwk,
            Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub.jwk[/] [grey](JWK string)[/]");

        // NOTE: this is a GitHub-specific command, so we hardcode the audience. Eventually, if 
        // we support other sponsoring platforms, we'd have other commands.
        var manifest = new SponsorableManifest(new Uri(settings.Issuer), new Uri($"https://github.com/{audience}"), settings.ClientId, new RsaSecurityKey(rsa), pub64);

        // Serialize the token and return as a string
        var jwt = manifest.ToJwt();

        var sponsorable = new FileInfo("sponsorable.jwt");
        await File.WriteAllTextAsync(sponsorable.FullName, jwt, Encoding.UTF8);

        AnsiConsole.MarkupLine($":check_mark_button: Generated new sponsorable JWT");
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{sponsorable.FullName}[/] [grey](upload to .github repo)[/]");
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
            ValidAudience = manifest.Audience,
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
