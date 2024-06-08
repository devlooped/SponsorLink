using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Principal;
using System.Text;
using Microsoft.VisualBasic;
using Spectre.Console;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly.Strings;

namespace Devlooped.Sponsors;

[Description("Removes all manifests and notifies issuers to remove backend data too.")]
public class RemoveCommand(IHttpClientFactory httpFactory, IGitHubAppAuthenticator authenticator) : AsyncCommand<RemoveCommand.RemoveSettings>
{
    public class RemoveSettings : CommandSettings
    {
        [Description("Sponsored account(s) to synchronize.")]
        [CommandArgument(0, "[sponsorable]")]
        public string[]? Sponsorable { get; set; }

        [Description("All manifests found locally should be removed.")]
        [CommandOption("-a|--all")]
        public bool? All { get; set; }

        /// <summary>
        /// Property used to modify the namespace from tests for scoping stored passwords.
        /// </summary>
        [CommandOption("--namespace", IsHidden = true)]
        public string Namespace { get; set; } = GitHubAppAuthenticator.DefaultNamespace;

        public override ValidationResult Validate()
        {
            if (All != true && !(Sponsorable?.Length > 0))
                return ValidationResult.Error(Remove.AllOrSponsorable);

            if (All == true && Sponsorable?.Length > 0)
                return ValidationResult.Error(Remove.AllOrSponsorable);

            return ValidationResult.Success();
        }
    }


    public override async Task<int> ExecuteAsync(CommandContext context, RemoveSettings settings)
    {
        var sponsorables = new HashSet<string>();
        if (settings.Sponsorable != null)
            sponsorables.AddRange(settings.Sponsorable);

        var ghDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink", "github");
        if (!Directory.Exists(ghDir))
        {
            MarkupLine(Remove.NoSponsorables);
            return -1;
        }

        if (settings.All == true)
        {
            sponsorables.AddRange(Directory.EnumerateFiles(ghDir, "*.jwt")
                .Select(x => Path.GetFileNameWithoutExtension(x)!));
        }

        if (sponsorables.Count == 0)
        {
            MarkupLine(Remove.NoSponsorables);
            return -1;
        }

        await Status().StartAsync(Remove.Removing(sponsorables.First()), async ctx =>
        {
            using var http = httpFactory.CreateClient();

            foreach (var sponsorable in sponsorables)
            {
                var file = Path.Combine(ghDir, $"{sponsorable}.jwt");
                if (!File.Exists(file))
                {
                    MarkupLine(Remove.NotFound(sponsorable));
                    continue;
                }

                // Simple reading first to get issuer to retrieve the manifest
                var jwt = await File.ReadAllTextAsync(file, Encoding.UTF8);
                var handler = new JwtSecurityTokenHandler();
                if (string.IsNullOrEmpty(jwt) || !handler.CanReadToken(jwt))
                {
                    MarkupLine(Remove.Invalid(sponsorable));
                    File.Delete(file);
                    continue;
                }

                File.Delete(file);

                // Attempt to invoke DELETE /me from issuer.
                var token = handler.ReadJwtToken(jwt);
                var issuer = new Uri(new Uri(token.Issuer), "me");
                if (token.Claims.FirstOrDefault(x => x.Type == "client_id")?.Value is not string clientId)
                {
                    MarkupLine(Remove.Invalid(sponsorable));
                    File.Delete(file);
                    continue;
                }

                if (await authenticator.AuthenticateAsync(clientId, new Progress<string>(), false, settings.Namespace) is not string accessToken)
                {
                    MarkupLine(Remove.AuthMissing(sponsorable));
                    continue;
                }

                var request = new HttpRequestMessage(HttpMethod.Delete, issuer);
                request.Headers.Authorization = new("Bearer", accessToken);
                var response = await http.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    MarkupLine(Remove.Unauthorized(sponsorable));
                }
                else if (!response.IsSuccessStatusCode)
                {
                    MarkupLine(Remove.ServerError(sponsorable));
                }
                else
                {
                    MarkupLine(Remove.Deleted(sponsorable));
                }
            }
        });

        return 0;
    }
}
