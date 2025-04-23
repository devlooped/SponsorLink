using System.ComponentModel;
using System.Text;
using Humanizer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.Sponsors.ViewCommand;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly;

namespace Devlooped.Sponsors;

public class ViewSettings : ToSSettings
{
    [Description("Show detailed information about each manifest")]
    [CommandOption("-d|--details")]
    [DefaultValue(true)]
    public bool Details { get; set; } = true;
}

[Description("Validates and displays the active sponsor manifests, if any")]
public partial class ViewCommand(IHttpClientFactory clientFactory) : ViewCommand<SponsorableViewSettings>(clientFactory) 
{
    public class SponsorableViewSettings : ViewSettings, ISponsorableSettings
    {
        [Description("Optional sponsored account(s) to view")]
        [CommandArgument(0, "[account]")]
        public string[]? Sponsorable { get; set; }
    }
}

public abstract class ViewCommand<TSettings>(IHttpClientFactory clientFactory) : AsyncCommand<TSettings>
    where TSettings : ViewSettings, ISponsorableSettings, new()
{
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        var targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink");

        if (!Directory.Exists(targetDir))
            return 0;

        await Status().StartAsync(Strings.Validate.Validating, async ctx =>
        {
            using var http = clientFactory.CreateClient();

            foreach (var file in Directory.EnumerateFiles(targetDir, "*.jwt", SearchOption.AllDirectories))
            {
                var account = Path.GetFileNameWithoutExtension(file);
                if (settings.Sponsorable is not null && !settings.Sponsorable.Contains(account))
                    continue;

                var relative = string.Join(Path.DirectorySeparatorChar, file.Split(Path.DirectorySeparatorChar)[^2..]);
                ctx.Status(Strings.Validate.ValidatingManifest(relative));

                // Simple reading first to get issuer to retrieve the manifest
                var jwt = await File.ReadAllTextAsync(file, Encoding.UTF8);
                if (string.IsNullOrEmpty(jwt))
                {
                    MarkupLine(Strings.Validate.EmptyManifest(account, relative));
                    continue;
                }

                var sponsor = new JsonWebTokenHandler
                {
                    MapInboundClaims = false,
                    SetDefaultTimesOnTokenCreation = false,
                }.ReadJsonWebToken(jwt);

                var issuer = new Uri(new Uri(sponsor.Issuer), "jwt");
                var response = await http.GetAsync(issuer);
                if (!response.IsSuccessStatusCode)
                {
                    MarkupLine(Strings.Validate.NoManifest(account, issuer));
                    continue;
                }

                var sponsorable = new JsonWebTokenHandler
                {
                    MapInboundClaims = false,
                    SetDefaultTimesOnTokenCreation = false,
                }.ReadJsonWebToken(await response.Content.ReadAsStringAsync());

                var pub = sponsorable.Claims.FirstOrDefault(x => x.Type == "sub_jwk")?.Value;

                if (pub is null)
                {
                    MarkupLine(Strings.Validate.NoPublicKey(account));
                    continue;
                }

                var aud = sponsorable.Claims.Where(x => x.Type == "aud").Select(x => x.Value.TrimEnd('/')).ToArray();
                SecurityKey key;

                try
                {
                    key = JsonWebKey.Create(pub);
                }
                catch (ArgumentException)
                {
                    MarkupLine(Strings.Validate.InvalidPublicKey(account));
                    continue;
                }

                var validation = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    AudienceValidator = (audiences, token, parameters) => audiences.All(audience => aud.Any(uri => uri == audience.TrimEnd('/'))),
                    ValidIssuer = sponsorable.Issuer,
                    IssuerSigningKey = key,
                    RoleClaimType = "roles",
                    NameClaimType = "sub",
                };

                var result = await new JsonWebTokenHandler
                {
                    MapInboundClaims = false,
                    SetDefaultTimesOnTokenCreation = false,
                }.ValidateTokenAsync(jwt, validation);

                // create a switch statement for the different exceptions types we want to handle
                switch (result.Exception)
                {
                    case SecurityTokenExpiredException e:
                        MarkupLine(Strings.Validate.InvalidExpired(account, e.Expires.Humanize()));
                        break;
                    case SecurityTokenInvalidSignatureException:
                        MarkupLine(Strings.Validate.InvalidSignature(account));
                        break;
                    case SecurityTokenException:
                        MarkupLine(Strings.Validate.Invalid(account));
                        break;
                    case null:
                        var roles = result.ClaimsIdentity.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToHashSet();
                        MarkupLine(Strings.Validate.ValidExpires(account, result.SecurityToken.ValidTo.Humanize(), string.Join(", ", roles)));
                        break;
                }

                if (settings.Details)
                    Write(new Padder(sponsor.ToDetails(file), new Padding(3, 1, 0, 1)));
            }
        });

        return 0;
    }
}
