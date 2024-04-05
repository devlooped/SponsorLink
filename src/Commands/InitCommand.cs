using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Initializes a sponsorable manifest and token")]
public partial class InitCommand(Account user) : AsyncCommand<InitCommand.Settings>
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
        [CommandArgument(2, "[audience]")]
        public required string? Audience { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // Authenticated user must match GH user
        var principal = await Session.AuthenticateAsync();
        if (principal == null)
            return -1;

        if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Split('|')?[1], out var id))
        {
            AnsiConsole.MarkupLine(":cross_mark: Could not determine authenticated user id.");
            return -1;
        }

        if (user.Id != id)
        {
            AnsiConsole.MarkupLine($":cross_mark: SponsorLink authenticated user id ({id}) does not match GitHub CLI user id ({user.Id}).");
            return -1;
        }

        var audience = settings.Audience ?? user.Login;

        // Generate key pair
        var rsa = RSA.Create(2048);
        var pub = Convert.ToBase64String(rsa.ExportRSAPublicKey());

        var options = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    info =>
                    {
                        if (info.Type != typeof(JsonWebKey))
                            return;

                        foreach (var prop in info.Properties)
                        {
                            // Don't serialize empty lists, makes for more concise JWKs
                            prop.ShouldSerialize = (obj, value) =>
                                value is not null &&
                                (value is not IList<string> list || list.Count > 0);
                        }
                    }
                }
            }
        };

        AnsiConsole.MarkupLine($":check_mark_button: Generated new signing key");

        var baseName = Path.Combine(Directory.GetCurrentDirectory(), audience);

        await File.WriteAllBytesAsync($"{audience}.key", rsa.ExportRSAPrivateKey());
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key[/]     [grey](private key)[/]");

        await File.WriteAllTextAsync($"{audience}.key.txt",
            Convert.ToBase64String(rsa.ExportRSAPublicKey()),
            Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key.txt[/] [grey](base64-encoded)[/]");

        await File.WriteAllTextAsync($"{audience}.key.jwk",
            JsonSerializer.Serialize(
                JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(true))),
                options),
            Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.key.jwk[/] [grey](JWK string)[/]");

        await File.WriteAllBytesAsync($"{audience}.pub", rsa.ExportRSAPublicKey());
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub[/]     [grey](public key)[/]");

        await File.WriteAllTextAsync($"{audience}.pub.txt", pub, Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub.txt[/] [grey](base64-encoded)[/]");

        await File.WriteAllTextAsync($"{audience}.pub.jwk",
            JsonSerializer.Serialize(
                JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false))),
                options),
            Encoding.UTF8);
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{baseName}.pub.jwk[/] [grey](JWK string)[/]");

        var issuer = settings.Issuer.EndsWith('/') ? settings.Issuer : settings.Issuer + "/";
        var claims = new List<Claim>
        {
            new("client_id", settings.ClientId),
            new("pub", pub),
        };

        var securityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            signingCredentials: signingCredentials);

        // Serialize the token and return as a string
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        var sponsorable = new FileInfo("sponsorable.jwt");
        await File.WriteAllTextAsync(sponsorable.FullName, jwt, Encoding.UTF8);

        AnsiConsole.MarkupLine($":check_mark_button: Generated new sponsorable JWT");
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: [link]{sponsorable.FullName}[/] [grey](upload to .github repo)[/]");
        AnsiConsole.MarkupLine($"\t:magnifying_glass_tilted_right: [grey]{jwt}[/]");

        return 0;
    }
}
