using Spectre.Console;
using static Spectre.Console.AnsiConsole;

namespace Devlooped.Sponsors;

public static class GraphQueryClientExtensions
{
    /// <summary>
    /// Accounts current user contributions to sponsorable repos as a sponsorship. 
    /// This makes sense since otherwise, active contributors would still need to 
    /// sponsor the repo sponsorables to get access to the sponsor benefits.
    /// </summary>
    public static async Task<Dictionary<string, HashSet<string>>> GetUserContributionsAsync(this IGraphQueryClient client) => await Status().StartAsync("Querying user contributions", async ctx =>
    {
        var contributed = new Dictionary<string, HashSet<string>>();

        if (await client.QueryAsync(GraphQueries.ViewerContributedRepositories) is not { Length: > 0 } viewerContribs)
        {
            MarkupLine("[yellow]x[/] User has no repository contributions.");
            return contributed;
        }

        // Keeps the orgs we have already checked for org-wide funding options
        var checkedorgs = new HashSet<string>();
        using var http = new HttpClient();

        var totalChunks = (int)Math.Ceiling((double)viewerContribs.Length / 20);
        var currentChunk = 0;

        foreach (var ownerRepo in viewerContribs.Chunk(20))
        {
            currentChunk++;
            var funding = await client.QueryAsync(GraphQueries.Funding(ownerRepo));

            if (funding?.Length > 0)
            {
                foreach (var repo in funding)
                {
                    foreach (var sponsorable in repo.Sponsorables)
                    {
                        contributed.TryAdd(sponsorable, []);
                        contributed[sponsorable].Add(repo.OwnerRepo);
                    }
                }
            }

            // Try org-level funding file
            foreach (var pair in ownerRepo)
            {
                var parts = pair.Split('/');
                if (parts.Length != 2)
                    continue;

                var owner = parts[0];
                var repo = parts[1];

                if (checkedorgs.Contains(owner))
                    continue;

                // Then try a org-level funding file, we only check it once per org.
                funding = await client.QueryAsync(GraphQueries.Funding([$"{owner}/.github"]));
                if (funding?.Length > 0)
                {
                    foreach (var gh in funding)
                    {
                        foreach (var sponsorable in gh.Sponsorables)
                        {
                            contributed.TryAdd(sponsorable, []);
                            contributed[sponsorable].Add(gh.OwnerRepo);
                        }
                    }
                }
                checkedorgs.Add(owner);
            }

            // Calculate percentage and update status
            var percentage = (int)Math.Round((double)currentChunk / totalChunks * 100);
            ctx.Status($"Querying user contributions [grey]({percentage}%)[/]");
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
