using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

[Description("Initializes a sponsorable manifest and token")]
public partial class InitCommand(Account user) : AsyncCommand<InitCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("The OpenID issuer URL, used to fetch OpenID configuration automatically.")]
        [CommandArgument(0, "<issuer>")]
        public required string Issuer { get; init; }

        [Description("The base URL of the deployed SponsorLink API that can initialize and sign manifests.")]
        [CommandArgument(0, "<audience>")]
        public required string Audience { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var status = AnsiConsole.Status();

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

        // Generate key pair
        var rsa = RSA.Create(2048);

        var keyFile = new FileInfo("signing.key");
        var pubFile = new FileInfo("signing.pub");

        await File.WriteAllBytesAsync(keyFile.FullName, rsa.ExportRSAPrivateKey());
        await File.WriteAllTextAsync(keyFile.FullName + ".txt", Convert.ToBase64String(rsa.ExportRSAPrivateKey()));
        await File.WriteAllBytesAsync(pubFile.FullName, rsa.ExportRSAPublicKey());
        await File.WriteAllTextAsync(pubFile.FullName + ".txt", Convert.ToBase64String(rsa.ExportRSAPublicKey()));

        AnsiConsole.MarkupLine($":check_mark_button: Generated new signing key");
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: {keyFile.FullName}");
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: {keyFile.FullName}.txt (base64-encoded)");
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: {pubFile.FullName}");
        AnsiConsole.MarkupLine($"\t:backhand_index_pointing_right: {pubFile.FullName}.txt (base64-encoded)");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Variables.AccessToken);

        var baseUri = new Uri(settings.Audience.EndsWith('/') ? settings.Audience : settings.Audience + "/");

        var payload = new
        {
            iss = settings.Issuer.EndsWith('/') ? settings.Issuer : settings.Issuer + "/",
            aud = baseUri.AbsoluteUri,
            pub = Convert.ToBase64String(rsa.ExportRSAPublicKey())
        };

        // NOTE: to test the local flow end to end, run the SponsorLink functions App project locally. You will 
        var url = Debugger.IsAttached ? "http://localhost:7288/init" : $"{baseUri.AbsoluteUri}init";

        var response = await status.StartAsync(ThisAssembly.Strings.Sync.Signing, async _
            => await http.PostAsJsonAsync(url, payload));

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            AnsiConsole.MarkupLine(":cross_mark: Could not create new manifest: unauthorized.");
            return -1;
        }
        else if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (content is { Length: > 0 })
                content = $" ({content})";

            AnsiConsole.MarkupLine($":cross_mark: Could not sign new manifest: {response.StatusCode}{content}");
            return -1;
        }

        // Attempt to validate the JWT we just got against the known public key from SL
        var token = await response.Content.ReadAsStringAsync();
        var validation = new TokenValidationParameters
        {
            IgnoreTrailingSlashWhenValidatingAudience = true,
            ValidAudience = payload.aud,
            ValidateAudience = true,
            ValidIssuer = "https://sponsorlink.us.auth0.com/",
            ValidateIssuer = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(SponsorLink.PublicKey)
        };

        try
        {
#if DEBUG
            IdentityModelEventSource.ShowPII = true;
#endif

            new JwtSecurityTokenHandler().ValidateToken(token, validation, out var validated);

            var tokenFile = new FileInfo("sponsorlink.jwt");
            await File.WriteAllTextAsync(tokenFile.FullName, token);
            AnsiConsole.MarkupLine($":check_mark_button: Persisted new sponsorable token :backhand_index_pointing_right: {tokenFile.FullName}");

            var jsonFile = new FileInfo("sponsorlink.json");
            await File.WriteAllTextAsync(jsonFile.FullName,
                JsonSerializer.Serialize(payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

            AnsiConsole.MarkupLine($":check_mark_button: Persisted new sponsorable manifest :backhand_index_pointing_right: {jsonFile.FullName}");
            AnsiConsole.MarkupLine($":information: Please upload both files to your [lime][[user/org]]/.github[/] repository.");

            return 0;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            AnsiConsole.MarkupLine(":cross_mark: The manifest signature is invalid.");
            return -2;
        }
        catch (SecurityTokenException ex)
        {
            AnsiConsole.MarkupLine($":cross_mark: The manifest is invalid.");
            AnsiConsole.WriteException(ex);
            return -3;
        }
    }
}
