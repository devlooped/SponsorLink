using System.ComponentModel;
using System.Diagnostics;
using DotNetConfig;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly.Strings;

namespace Devlooped.Sponsors;

[Description("Synchronizes the sponsorships manifest")]
public partial class SyncCommand(ICommandApp app, IGraphQueryClient client, IGitHubDeviceAuthenticator authenticator, IHttpClientFactory httpFactory) : GitHubAsyncCommand<SyncCommand.SyncSettings>(app)
{
    public class SyncSettings : CommandSettings
    {
        [Description("Optional sponsored account(s) to synchronize.")]
        [CommandArgument(0, "[sponsorable]")]
        public string[]? Sponsorable { get; set; }

        [CommandOption("-i|--issuer", IsHidden = true)]
        public string? Issuer { get; set; }

        [Description("Enable or disable automatic synchronization of expired manifests.")]
        [CommandOption("--autosync")]
        public bool? AutoSync { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SyncSettings settings)
    {
        var result = await base.ExecuteAsync(context, settings);
        if (result != 0)
            return result;

        Debug.Assert(Account != null, "After authentication, Account should never be null.");

        var sponsorables = new HashSet<string>();
        if (settings.Sponsorable?.Length > 0)
        {
            sponsorables.AddRange(settings.Sponsorable);
        }
        else
        {
            // Discover all candidate sponsorables for the current user
            if (await Status().StartAsync(Sync.QueryingUserSponsorships, async _ =>
            {
                if (await client.QueryAsync(GraphQueries.ViewerSponsored) is not { } sponsored)
                {
                    MarkupLine(Sync.QueryingUserSponsorshipsFailed);
                    return -1;
                }
                sponsorables.AddRange(sponsored);
                return 0;
            }) == -1)
            {
                return -1;
            }

            sponsorables.AddRange((await client.GetUserContributionsAsync()).Keys);

            var orgs = Array.Empty<Organization>();

            if (await Status().StartAsync(Sync.QueryingUserOrgs, async _ =>
            {
                if (await client.QueryAsync(GraphQueries.ViewerOrganizations) is not { } viewerorgs)
                {
                    MarkupLine(Sync.QueryingUserOrgsFailed);
                    return -1;
                }
                orgs = viewerorgs;
                return 0;
            }) == -1)
            {
                return -1;
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

        var manifests = new List<SponsorableManifest>();

        await Status().StartAsync(Sync.FetchingManifests(sponsorables.Count), async ctx =>
        {
            foreach (var sponsorable in sponsorables)
            {
                ctx.Status = Sync.DetectingManifest(sponsorable);
                using var http = httpFactory.CreateClient();
                var (status, manifest) = await SponsorableManifest.FetchAsync(sponsorable, http);
                switch (status)
                {
                    case SponsorableManifest.Status.OK when manifest != null:
                        manifests.Add(manifest);
                        break;
                    case SponsorableManifest.Status.NotFound:
                        break;
                    default:
                        MarkupLine(Sync.InvalidManifest(sponsorable, status));
                        break;
                }
            }
        });

        var maxlength = manifests.MaxBy(x => x.Sponsorable.Length)?.Sponsorable.Length ?? 10;
        var interactive = new List<SponsorableManifest>();
        var targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink", "github");
        Directory.CreateDirectory(targetDir);

        // Sync non-interactive (pre-authenticated) manifests first.
        foreach (var manifest in manifests)
        {
            if (await authenticator.AuthenticateAsync(manifest.ClientId, new Progress<string>(), false) is not { Length: > 0 } token)
            {
                interactive.Add(manifest);
                continue;
            }

            var (status, jwt) = await SponsorManifest.FetchAsync(manifest, token);
            if (status == SponsorManifest.Status.NotSponsoring)
            {
                var links = string.Join(", ", manifest.Audience.Select(x => $"[link]{x}[/]"));
                MarkupLine(Sync.ConsiderSponsoring(manifest.Sponsorable.PadRight(maxlength), links));
                continue;
            }

            if (status == SponsorManifest.Status.Success)
            {
                File.WriteAllText(Path.Combine(targetDir, manifest.Sponsorable + ".jwt"), jwt);
                MarkupLine(Sync.Thanks(manifest.Sponsorable.PadRight(maxlength)));
            }
            else
            {
                MarkupLine(Sync.Failed(manifest.Sponsorable.PadRight(maxlength)));
                continue;
            }
        }

        if (interactive.Count > 0 && Confirm(interactive.Count == 1 ? Sync.InteractiveAuthNeeded1(interactive[0].Sponsorable) : Sync.InteractiveAuthNeeded(interactive.Count)))
        {
            maxlength = interactive.MaxBy(x => x.Sponsorable.Length)?.Sponsorable.Length ?? 10;

            // Sync interactive, which requires user intervention.
            foreach (var manifest in interactive)
            {
                await Status().StartAsync(Sync.Synchronizing(manifest.Sponsorable), async ctx =>
                {
                    var progress = new Progress<string>(x => ctx.Status(Sync.SynchronizingProgress(manifest.Sponsorable, x)));
                    var token = await authenticator.AuthenticateAsync(manifest.ClientId, progress, true);
                    if (string.IsNullOrEmpty(token))
                        return;

                    var (status, jwt) = await SponsorManifest.FetchAsync(manifest, token);
                    if (status == SponsorManifest.Status.NotSponsoring)
                    {
                        var links = string.Join(", ", manifest.Audience.Select(x => $"[link]{x}[/]"));
                        MarkupLine(Sync.ConsiderSponsoring(manifest.Sponsorable.PadRight(maxlength), links));
                    }
                    else if (status == SponsorManifest.Status.Success)
                    {
                        File.WriteAllText(Path.Combine(targetDir, manifest.Sponsorable + ".jwt"), jwt);
                        MarkupLine(Sync.Thanks(manifest.Sponsorable.PadRight(maxlength)));
                    }
                    else
                    {
                        MarkupLine(Sync.Failed(manifest.Sponsorable.PadRight(maxlength)));
                    }
                });
            }
        }

        var config = Config.Build(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink"));
        var autosync = settings.AutoSync;

        if (settings.AutoSync == null && 
            !config.TryGetBoolean("sponsorlink", "autosync", out _) && 
            Confirm(Sync.AutoSync))
        {
            autosync = true;
        }
        // NOTE: we'd continue to ask for auto-sync even if they responded no, so they can change their mind.

        if (autosync != null)
        {
            config.SetBoolean("sponsorlink", "autosync", autosync.Value);
            if (autosync == true) 
                MarkupLine(Sync.AutoSyncEnabled);
            else
                MarkupLine(Sync.AutoSyncDisabled);
        }

        WriteLine("Done");

        return 0;
    }
}