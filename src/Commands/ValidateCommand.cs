using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly.Strings;

namespace Devlooped.Sponsors;

[Description("Validates the active sponsor manifests, if any")]
public partial class ValidateCommand(IHttpClientFactory clientFactory) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink");

        if (!Directory.Exists(targetDir))
            return 0;

        await Status().StartAsync(Validate.Validating, async ctx =>
        {
            using var http = clientFactory.CreateClient();

            foreach (var file in Directory.EnumerateFiles(targetDir, "*.jwt", SearchOption.AllDirectories))
            {
                var account = Path.GetFileNameWithoutExtension(file);
                var relative = string.Join(Path.DirectorySeparatorChar, file.Split(Path.DirectorySeparatorChar)[^2..]);
                ctx.Status(Validate.ValidatingManifest(relative));

                // Simple reading first to get issuer to retrieve the manifest
                var jwt = await File.ReadAllTextAsync(file, Encoding.UTF8);
                if (string.IsNullOrEmpty(jwt))
                {
                    MarkupLine(Validate.EmptyManifest(account, relative));
                    continue;
                }

                var sponsor = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
                var issuer = new Uri(new Uri(sponsor.Issuer), "jwt");
                var response = await http.GetAsync(issuer);
                if (!response.IsSuccessStatusCode)
                {
                    MarkupLine(Validate.NoManifest(account, issuer));
                    continue;
                }

                var sponsorable = new JwtSecurityTokenHandler().ReadJwtToken(await response.Content.ReadAsStringAsync());
                var pub = sponsorable.Claims.FirstOrDefault(x => x.Type == "pub")?.Value;

                if (pub is null)
                {
                    MarkupLine(Validate.NoPublicKey(account));
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
                    IssuerSigningKey = new RsaSecurityKey(key)
                };

                try
                {
                    var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, validation, out var token);
                    var roles = principal.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToHashSet();

                    MarkupLine(Validate.ValidExpires(account, token.ValidTo.ToString("yyyy-MM-dd"), string.Join(", ", roles)));
                }
                catch (SecurityTokenExpiredException e)
                {
                    MarkupLine(Validate.InvalidExpired(account, e.Expires.ToString("yyyy-MM-dd")));
                }
                catch (SecurityTokenInvalidSignatureException)
                {
                    MarkupLine(Validate.InvalidSignature(account));
                }
                catch (SecurityTokenException)
                {
                    MarkupLine(Validate.Invalid(account));
                }
            }
        });

        return 0;
    }
}
