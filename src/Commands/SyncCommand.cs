using System.ComponentModel;
using System.Diagnostics;
using SharpYaml;
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

            sponsorables.AddRange(await GetUserContributions());

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
                    ctx.Status(Sync.QueryingOrgPublicSponsorships(org));
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

        if (interactive.Count > 0)
        {

            //if (AnsiConsole.Confirm($"[lime]?[/] [white] Allow read-access to your public GitHub profile to {sponsorable}?[/] (Required for sponsor manifest sync)"))
            //    return await authenticator.AuthenticateAsync(clientId, progress, true);
        }

        // Sync interactive, which requires user intervention.
        foreach (var manifest in interactive)
        {
            //if (AnsiConsole.Confirm($"[lime]?[/] [white] Allow read-access to your public GitHub profile to {sponsorable}?[/] (Required for sponsor manifest sync)"))
            //    return await authenticator.AuthenticateAsync(clientId, progress, true);
        }

        WriteLine("Done");

        #region Old
        // If we end up with no sponsorships whatesover, no-op and exit.
        //if (usersponsored.Count == 0 && orgsponsored.Count == 0 && usercontribs.Count == 0)
        //{
        //    AnsiConsole.MarkupLine($"[yellow]![/] User {user.Login} (or any of the organizations they long to) is not currently sponsoring any accounts, or contributing to any sponsorable repositories.");
        //    return 0;
        //}

        //AnsiConsole.MarkupLine($"[green]✓[/] Found {usersponsored.Count} personal sponsorships, {usercontribs.Count} indirect sponsorships via contributions, and {orgsponsored.Count} organization sponsorships.");

        //// Create unsigned manifest locally, for back-end validation
        //var manifest = Manifest.Create(Session.InstallationId, user.Id.ToString(), user.Emails, domains.ToArray(),
        //    new HashSet<string>(usersponsored.Concat(orgsponsored).Concat(usercontribs)).ToArray());

        //using var http = new HttpClient();
        //http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Variables.AccessToken);

        //// NOTE: to test the local flow end to end, run the SponsorLink functions App project locally. You will 
        //var url = Debugger.IsAttached ? "http://localhost:7288/sign" : "https://sponsorlink.devlooped.com/sign";

        //var response = await status.StartAsync(ThisAssembly.Strings.Sync.Signing, async _
        //    => await http.PostAsync(url, new StringContent(manifest.Token)));

        //if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        //{
        //    AnsiConsole.MarkupLine("[red]x[/] Could not sign manifest: unauthorized.");
        //    return -1;
        //}
        //else if (!response.IsSuccessStatusCode)
        //{
        //    AnsiConsole.MarkupLine($"[red]x[/] Could not sign manifest: {response.StatusCode} ({await response.Content.ReadAsStringAsync()}).");
        //    return -1;
        //}

        //var signed = await response.Content.ReadAsStringAsync();
        //var signedManifest = Manifest.Read(signed, Session.InstallationId);

        //// Make sure we can read it back
        //Debug.Assert(manifest.Hashes.SequenceEqual(signedManifest.Hashes));

        //AnsiConsole.MarkupLine($"[green]✓[/] Got signed manifest, expires on {signedManifest.ExpiresAt:yyyy-MM-dd}.");

        //Variables.Manifest = signed;

        //var tree = new Tree(new Text(Emoji.Replace(":purple_heart: Sponsorships"), new Style(Color.MediumPurple2)));

        //if (usersponsored != null)
        //{
        //    var node = new TreeNode(new Text(user.Login, new Style(Color.Blue)));
        //    node.AddNodes(usersponsored);

        //    if (usercontribs.Count > 0)
        //    {
        //        var contrib = new TreeNode(new Text("contributed to", new Style(Color.Green)));
        //        contrib.AddNodes(usercontribs);
        //        node.AddNode(contrib);
        //    }

        //    tree.AddNode(node);
        //}


        //foreach (var org in orgsponsors)
        //{
        //    var node = new TreeNode(new Text(org.Login, new Style(Color.Green)));
        //    node.AddNodes(org.Sponsorables);
        //    tree.AddNode(node);
        //}

        //AnsiConsole.Write(tree);
        #endregion

        return 0;
    }

    /// <summary>
    /// Accounts current user contributions to sponsorable repos as a sponsorship. 
    /// This makes sense since otherwise, active contributors would still need to 
    /// sponsor the repo sponsorables to get access to the sponsor benefits.
    /// </summary>
    async Task<HashSet<string>> GetUserContributions() => await Status().StartAsync("Querying user contributions", async ctx =>
    {
        var contributed = new HashSet<string>();

        if (await client.QueryAsync(GraphQueries.ViewerContributedRepositories) is not { Length: > 0 } viewerContribs)
        {
            MarkupLine("[yellow]x[/] User has no repository contributions.");
            return contributed;
        }

        // Keeps the orgs we have already checked for org-wide funding options
        var checkedorgs = new HashSet<string>();
        var serializer = new SharpYaml.Serialization.Serializer(new SharpYaml.Serialization.SerializerSettings
        {
            IgnoreUnmatchedProperties = true,
        });

        using var http = new HttpClient();

        async Task AddContributedAsync(string ownerRepo)
        {
            if (await http!.GetAsync($"https://github.com/{ownerRepo}/raw/main/.github/FUNDING.yml") is { IsSuccessStatusCode: true } repoFunding)
            {
                var yml = await repoFunding.Content.ReadAsStringAsync();
                try
                {
                    if (serializer!.Deserialize<SingleSponsorable>(yml) is { github: not null } single)
                    {
                        contributed?.Add(single.github);
                    }
                }
                catch (YamlException)
                {
                    try
                    {
                        if (serializer!.Deserialize<MultipleSponsorable>(yml) is { github.Length: > 0 } multiple)
                        {
                            foreach (var account in multiple.github)
                            {
                                contributed?.Add(account);
                            }
                        }
                    }
                    catch (YamlException) { }
                }
            }
        }

        foreach (var ownerRepo in viewerContribs)
        {
            var parts = ownerRepo.Split('/');
            if (parts.Length != 2)
                continue;

            var owner = parts[0];
            var repo = parts[1];

            ctx.Status($"Discovering {ownerRepo} funding options");

            // First try a repo-level funding file
            await AddContributedAsync(ownerRepo);

            if (checkedorgs.Contains(owner))
                continue;

            // Then try a org-level funding file, we only check it once per org.
            await AddContributedAsync(owner + "/.github");
            checkedorgs.Add(owner);
        }

        return contributed;
    });

    record SingleSponsorable
    {
        public string? github { get; init; }
    }

    record MultipleSponsorable
    {
        public string[]? github { get; init; }
    }
}
