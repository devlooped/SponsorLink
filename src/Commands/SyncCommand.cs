using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DotNetConfig;
using Spectre.Console;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly.Strings;

namespace Devlooped.Sponsors;

[Description("Synchronizes sponsorship manifests")]
public partial class SyncCommand(ICommandApp app, DotNetConfig.Config config, IGraphQueryClient client, IGitHubAppAuthenticator authenticator, IHttpClientFactory httpFactory) : GitHubAsyncCommand<SyncCommand.SyncSettings>(app, config)
{
    public static class ErrorCodes
    {
        public const int UnauthenticatedGitHubCLI = -1;
        public const int GraphDiscoveryFailure = -2;
        public const int SponsorableManifestNotFound = -3;
        public const int SponsorableManifestInvalid = -4;
        public const int InteractiveAuthRequired = -5;
        public const int NotSponsoring = -6;
        public const int SyncFailure = -7;
    }

    public class SyncSettings : ToSSettings
    {
        [Description("Optional sponsored account(s) to synchronize.")]
        [CommandArgument(0, "[account]")]
        public string[]? Sponsorable { get; set; }

        [CommandOption("-i|--issuer", IsHidden = true)]
        public string? Issuer { get; set; }

        [Description("Enable or disable automatic synchronization of expired manifests.")]
        [CommandOption("--autosync")]
        public bool? AutoSync { get; set; }

        [Description("Whether to prevent interactive credentials refresh.")]
        [DefaultValue(false)]
        [CommandOption("-u|--unattended")]
        public bool Unattended { get; set; }

        [Description("Sync only existing local manifests.")]
        [DefaultValue(false)]
        [CommandOption("-l|--local")]
        public bool LocalDiscovery { get; set; }

        /// <summary>
        /// Property used to modify the namespace from tests for scoping stored passwords.
        /// </summary>
        [CommandOption("--namespace", IsHidden = true)]
        public string Namespace { get; set; } = GitHubAppAuthenticator.DefaultNamespace;

        public override ValidationResult Validate()
        {
            if (Sponsorable?.Length > 0 && LocalDiscovery)
                return ValidationResult.Error(Sync.LocalOrSponsorables);

            return base.Validate();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SyncSettings settings)
    {
        // NOTE: ToS acceptance ALWAYS runs from Program.cs
        var result = 0;
        var ghDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink", "github");
        Directory.CreateDirectory(ghDir);

        var sponsorables = new HashSet<string>();
        if (settings.Sponsorable?.Length > 0)
        {
            sponsorables.AddRange(settings.Sponsorable);
        }
        else if (settings.LocalDiscovery)
        {
            sponsorables.AddRange(Directory.EnumerateFiles(ghDir, "*.jwt")
                .Select(x => Path.GetFileNameWithoutExtension(x)!));
        }
        else
        {
            var nonInteractive = !Environment.UserInteractive || System.Console.IsInputRedirected || System.Console.IsOutputRedirected;
            var runDiscovery = nonInteractive || Confirm(Sync.AutomaticDiscovery);
            if (runDiscovery)
            {
                // In order to run discovery, we need an authenticated user
                result = await base.ExecuteAsync(context, settings);
                if (result != 0)
                    return result;

                // Discover all candidate sponsorables for the current user
                if (await Status().StartAsync(Sync.QueryingUserSponsorships, async _ =>
                {
                    if (await client.QueryAsync(GraphQueries.ViewerSponsored) is not { } sponsored)
                    {
                        MarkupLine(Sync.QueryingUserSponsorshipsFailed);
                        return ErrorCodes.GraphDiscoveryFailure;
                    }
                    sponsorables.AddRange(sponsored);
                    return 0;
                }) == ErrorCodes.GraphDiscoveryFailure)
                {
                    return ErrorCodes.GraphDiscoveryFailure;
                }

                sponsorables.AddRange((await client.GetUserContributionsAsync()).Keys);

                var orgs = Array.Empty<Organization>();

                if (await Status().StartAsync(Sync.QueryingUserOrgs, async _ =>
                {
                    if (await client.QueryAsync(GraphQueries.ViewerOrganizations) is not { } viewerorgs)
                    {
                        MarkupLine(Sync.QueryingUserOrgsFailed);
                        return ErrorCodes.GraphDiscoveryFailure;
                    }
                    orgs = viewerorgs;
                    return 0;
                }) == ErrorCodes.GraphDiscoveryFailure)
                {
                    return ErrorCodes.GraphDiscoveryFailure;
                }

                // Collect org-sponsored accounts. NOTE: we'll typically only get public sponsorships 
                // since the current user would typically NOT be an admin of these orgs. 
                // But they can always specify the orgs they are interested in via the command line.
                await Status().StartAsync(Sync.QueryingUserOrgSponsorships, async ctx =>
                {
                    foreach (var org in orgs)
                    {
                        ctx.Status(Sync.QueryingOrgPublicSponsorships(org.Login));
                        if (await client.QueryAsync(GraphQueries.OrganizationSponsorships(org.Login)) is { Length: > 0 } sponsored)
                        {
                            sponsorables.AddRange(sponsored);
                        }
                    }
                });
            }
        }

        var manifests = new List<SponsorableManifest>();

        await Status().StartAsync(Sync.FetchingManifests(sponsorables.Count), async ctx =>
        {
            foreach (var sponsorable in sponsorables)
            {
                ctx.Status = Sync.DetectingManifest(sponsorable);
                using var http = httpFactory.CreateClient();

                // First try default branch
                var branch = "main";
                var (status, manifest) = await SponsorableManifest.FetchAsync(sponsorable, branch, http);
                if (status == SponsorableManifest.Status.NotFound)
                {
                    // We can directly query via the default HTTP client since the .github repository must be public.
                    branch = await httpFactory.GetQueryClient().QueryAsync(new GraphQuery(
                        $"/repos/{sponsorable}/.github", ".default_branch") { IsLegacy = true });

                    if (branch != null && branch != "main")
                        // Retry discovery with non-'main' branch
                        (status, manifest) = await SponsorableManifest.FetchAsync(sponsorable, branch, http);
                }

                switch (status)
                {
                    case SponsorableManifest.Status.OK when manifest != null:
                        manifests.Add(manifest);
                        break;
                    case SponsorableManifest.Status.NotFound:
                        result = ErrorCodes.SponsorableManifestNotFound;
                        break;
                    default:
                        MarkupLine(Sync.InvalidManifest(sponsorable, status));
                        result = ErrorCodes.SponsorableManifestInvalid;
                        break;
                }
            }
        });

        var maxlength = manifests.MaxBy(x => x.Sponsorable.Length)?.Sponsorable.Length ?? 10;
        var interactive = new List<SponsorableManifest>();

        // Sync non-interactive (pre-authenticated) manifests first.
        foreach (var manifest in manifests)
        {
            if (await authenticator.AuthenticateAsync(manifest.ClientId, new Progress<string>(), false, settings.Namespace) is not { Length: > 0 } token)
            {
                interactive.Add(manifest);
                // If running unattended, we won't be able to proceed with interactive auth, so set fail return code.
                if (settings.Unattended)
                {
                    MarkupLine(Sync.UnattendedWithInteractiveAuth(manifest.Sponsorable.PadRight(maxlength)));
                    result = ErrorCodes.InteractiveAuthRequired;
                }
                continue;
            }

            var (status, jwt) = await Status().StartAsync(Sync.Synchronizing(manifest.Sponsorable), async ctx => await SponsorManifest.FetchAsync(manifest, token));
            if (status == SponsorManifest.Status.NotSponsoring)
            {
                var links = string.Join(", ", manifest.Audience.Select(x => $"[link]{x}[/]"));
                MarkupLine(Sync.ConsiderSponsoring(manifest.Sponsorable.PadRight(maxlength), links));
                result = ErrorCodes.NotSponsoring;
                continue;
            }

            if (status == SponsorManifest.Status.Success)
            {
                File.WriteAllText(Path.Combine(ghDir, manifest.Sponsorable + ".jwt"), jwt, Encoding.UTF8);
                var roles = new JwtSecurityTokenHandler()
                {
                    MapInboundClaims = false
                }.ReadJwtToken(jwt).Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToHashSet();

                MarkupLine(Sync.Thanks(manifest.Sponsorable.PadRight(maxlength), string.Join(", ", roles)));
            }
            else
            {
                MarkupLine(Sync.Failed(manifest.Sponsorable.PadRight(maxlength)));
                result = ErrorCodes.SyncFailure;
                continue;
            }
        }

        if (!settings.Unattended && interactive.Count > 0 && Confirm(interactive.Count == 1 ? Sync.InteractiveAuthNeeded1(interactive[0].Sponsorable) : Sync.InteractiveAuthNeeded(interactive.Count)))
        {
            maxlength = interactive.MaxBy(x => x.Sponsorable.Length)?.Sponsorable.Length ?? 10;

            // Sync interactive, which requires user intervention.
            foreach (var manifest in interactive)
            {
                await Status().StartAsync(Sync.Synchronizing(manifest.Sponsorable), async ctx =>
                {
                    var progress = new Progress<string>(x => ctx.Status(Sync.SynchronizingProgress(manifest.Sponsorable, x)));
                    var token = await authenticator.AuthenticateAsync(manifest.ClientId, progress, true, settings.Namespace);
                    if (string.IsNullOrEmpty(token))
                        return;

                    var (status, jwt) = await SponsorManifest.FetchAsync(manifest, token);
                    if (status == SponsorManifest.Status.NotSponsoring)
                    {
                        var links = string.Join(", ", manifest.Audience.Select(x => $"[link]{x}[/]"));
                        MarkupLine(Sync.ConsiderSponsoring(manifest.Sponsorable.PadRight(maxlength), links));
                        result = ErrorCodes.NotSponsoring;
                    }
                    else if (status == SponsorManifest.Status.Success)
                    {
                        File.WriteAllText(Path.Combine(ghDir, manifest.Sponsorable + ".jwt"), jwt);
                        var roles = new JwtSecurityTokenHandler()
                        {
                            MapInboundClaims = false
                        }.ReadJwtToken(jwt).Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToHashSet();

                        MarkupLine(Sync.Thanks(manifest.Sponsorable.PadRight(maxlength), string.Join(", ", roles)));
                    }
                    else
                    {
                        MarkupLine(Sync.Failed(manifest.Sponsorable.PadRight(maxlength)));
                        result = ErrorCodes.SyncFailure;
                    }
                });
            }
        }

        var autosync = settings.AutoSync;

        if (!settings.Unattended &&
            settings.AutoSync == null &&
            !config.TryGetBoolean("sponsorlink", "autosync", out _) &&
            Confirm(Sync.AutoSync))
        {
            autosync = true;
        }
        // NOTE: we'd continue to ask for auto-sync even if they responded no, so they can change their mind.

        // Let the config be handled by the config command for consistency.
        if (autosync != null)
            new ConfigCommand(config).Execute(context, new ConfigCommand.ConfigSettings { AutoSync = autosync });

        return result;
    }
}