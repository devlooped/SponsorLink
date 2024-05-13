using System;
using SharpYaml;
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
        var serializer = new SharpYaml.Serialization.Serializer(new SharpYaml.Serialization.SerializerSettings
        {
            IgnoreUnmatchedProperties = true,
        });

        using var http = new HttpClient();

        async Task AddContributedAsync(string ownerRepo)
        {
            var parts = ownerRepo.Split('/');
            if (parts.Length != 2)
                return;

            var branch = await client.QueryAsync(GraphQueries.DefaultBranch(parts[0], parts[1]));
            if (branch is null)
                return;

            if (await http!.GetAsync($"https://github.com/{ownerRepo}/raw/{branch}/.github/FUNDING.yml") is { IsSuccessStatusCode: true } repoFunding)
            {
                var yml = await repoFunding.Content.ReadAsStringAsync();

                try
                {
                    if (serializer!.Deserialize<SingleSponsorable>(yml) is { github: not null } single)
                    {
                        contributed.TryAdd(single.github, []);
                        contributed[single.github].Add(ownerRepo);
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
                                contributed.TryAdd(account, []);
                                contributed[account].Add(ownerRepo);
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
