using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Humanizer;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly;

namespace Devlooped.Sponsors;

[Description("Validates and displays the active sponsor manifests, if any")]
public partial class ViewCommand(IHttpClientFactory clientFactory) : AsyncCommand<ViewCommand.ViewSettings>
{
    public class ViewSettings : ToSSettings
    {
        [Description("Show detailed information about each manifest")]
        [CommandOption("-d|--details")]
        [DefaultValue(true)]
        public bool Details { get; set; } = true;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ViewSettings settings)
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
                var relative = string.Join(Path.DirectorySeparatorChar, file.Split(Path.DirectorySeparatorChar)[^2..]);
                ctx.Status(Strings.Validate.ValidatingManifest(relative));

                // Simple reading first to get issuer to retrieve the manifest
                var jwt = await File.ReadAllTextAsync(file, Encoding.UTF8);
                if (string.IsNullOrEmpty(jwt))
                {
                    MarkupLine(Strings.Validate.EmptyManifest(account, relative));
                    continue;
                }

                var sponsor = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
                var issuer = new Uri(new Uri(sponsor.Issuer), "jwt");
                var response = await http.GetAsync(issuer);
                if (!response.IsSuccessStatusCode)
                {
                    MarkupLine(Strings.Validate.NoManifest(account, issuer));
                    continue;
                }

                var sponsorable = new JwtSecurityTokenHandler().ReadJwtToken(await response.Content.ReadAsStringAsync());
                var pub = sponsorable.Claims.FirstOrDefault(x => x.Type == "pub")?.Value;

                if (pub is null)
                {
                    MarkupLine(Strings.Validate.NoPublicKey(account));
                    continue;
                }

                var aud = sponsorable.Claims.Where(x => x.Type == "aud").Select(x => x.Value.TrimEnd('/')).ToArray();

                var key = RSA.Create();
                key.ImportRSAPublicKey(Convert.FromBase64String(pub), out _);

                var validation = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    AudienceValidator = (audiences, token, parameters) => audiences.All(audience => aud.Any(uri => uri == audience.TrimEnd('/'))),
                    ValidIssuer = sponsorable.Issuer,
                    IssuerSigningKey = new RsaSecurityKey(key),
                    RoleClaimType = "roles",
                    NameClaimType = "sub",
                };

                try
                {
                    var principal = new JwtSecurityTokenHandler()
                    {
                        MapInboundClaims = false,
                    }.ValidateToken(jwt, validation, out var token);

                    var roles = principal.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToHashSet();

                    MarkupLine(Strings.Validate.ValidExpires(account, token.ValidTo.Humanize(), string.Join(", ", roles)));
                }
                catch (SecurityTokenExpiredException e)
                {
                    MarkupLine(Strings.Validate.InvalidExpired(account, e.Expires.Humanize()));
                }
                catch (SecurityTokenInvalidSignatureException)
                {
                    MarkupLine(Strings.Validate.InvalidSignature(account));
                }
                catch (SecurityTokenException)
                {
                    MarkupLine(Strings.Validate.Invalid(account));
                }
                finally
                {
                    if (settings.Details)
                        Write(new Padder(sponsor.ToDetails(file), new Padding(3, 1, 0, 1)));
                }
            }
        });

        return 0;
    }
}
