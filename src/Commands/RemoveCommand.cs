using System.ComponentModel;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Spectre.Console;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static Devlooped.Sponsors.ThisAssembly.Strings;

namespace Devlooped.Sponsors;

[Description("Removes manifests and notifies issuers to remove backend data too.")]
public class RemoveCommand(IHttpClientFactory httpFactory, IGitHubAppAuthenticator authenticator) : AsyncCommand<RemoveCommand.RemoveSettings>
{
    public class RemoveSettings : ToSSettings
    {
        [Description("Sponsored account(s) to remove.")]
        [CommandArgument(0, "[account]")]
        public string[]? Sponsorable { get; set; }

        [Description("Remove all accounts with cached local manifests.")]
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
            var creds = GitCredentialManager.CredentialManager.Create(settings.Namespace);

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
                var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };

                File.Delete(file);
                MarkupLine(Remove.Done(sponsorable));

                var padding = new Padding(3, 0, 0, 0);
                Write(new Padder(new Markup(Remove.DeletedManifest(Path.Combine("~", ".sponsorlink", "github", sponsorable + ".jwt"))), padding));

                if (string.IsNullOrEmpty(jwt) || !handler.CanReadToken(jwt))
                {
                    MarkupLine(Remove.InvalidManifest);
                    continue;
                }

                // Attempt to invoke DELETE /me from issuer.
                var token = handler.ReadJsonWebToken(jwt);
                var issuer = new Uri(new Uri(token.Issuer), "me");
                if (token.Claims.FirstOrDefault(x => x.Type == "client_id")?.Value is not string clientId)
                    continue;

                if (await authenticator.AuthenticateAsync(clientId, new Progress<string>(), false, settings.Namespace) is not string accessToken)
                {
                    Write(new Padder(new Markup(Remove.NoCredsFound(issuer, clientId)), padding));
                    continue;
                }

                var request = new HttpRequestMessage(HttpMethod.Delete, issuer);
                request.Headers.Authorization = new("Bearer", accessToken);
                var response = await http.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Write(new Padder(new Markup(Remove.Unauthorized(issuer)), padding));
                }
                else if (!response.IsSuccessStatusCode)
                {
                    Write(new Padder(new Markup(Remove.IssuerFailure(issuer)), padding));
                }
                else
                {
                    Write(new Padder(new Markup(Remove.DeleteEndpoint(issuer)), padding));
                }

                creds.Remove("https://github.com", clientId);
                Write(new Padder(new Markup(Remove.DeletedCreds(clientId)), padding));
            }
        });

        return 0;
    }
}
