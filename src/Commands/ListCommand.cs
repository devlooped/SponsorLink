﻿using System.ComponentModel;
using System.Diagnostics;
using DotNetConfig;
using GitCredentialManager;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.Sponsors.ListCommand;

namespace Devlooped.Sponsors;

[Description("Lists current user and organization sponsorships leveraging the GitHub CLI")]
public class ListCommand(Config config, IGraphQueryClient client) : GitHubAsyncCommand<ListSettings>(config)
{
    public class ListSettings : ToSSettings
    {
        [Description(@"Read GitHub authentication token from standard input for sync")]
        [DefaultValue(false)]
        [CommandOption("--with-token")]
        public bool WithToken { get; set; }
    }

    record Organization(string Login, string[] Sponsorables);

    public override async Task<int> ExecuteAsync(CommandContext context, ListSettings settings)
    {
        string? token = default;
        if (settings.WithToken)
            token = new StreamReader(Console.OpenStandardInput()).ReadToEnd().Trim();

        using var withToken = GitHub.WithToken(token);
        if (token != null && withToken == null)
        {
            AnsiConsole.MarkupLine(":cross_mark: [yellow]Invalid GitHub token provided[/]");
            return -1;
        }

        var result = await base.ExecuteAsync(context, settings);
        if (result != 0)
            return result;

        Debug.Assert(Account != null, "After authentication, Account should never be null.");

        var status = AnsiConsole.Status();

        var usersponsored = await status.StartAsync($"Querying [dim]{Account.Login}[/] sponsorships", _ => client.QueryAsync(GraphQueries.ViewerSponsorships));
        if (usersponsored == null)
        {
            AnsiConsole.MarkupLine(":cross_mark: Could not query GitHub for user sponsorships.");
            return -1;
        }

        var userorgs = await status.StartAsync($"Querying [dim]{Account.Login}[/] organizations", _ => client.QueryAsync(GraphQueries.ViewerOrganizations));
        if (userorgs == null)
        {
            AnsiConsole.MarkupLine(":cross_mark: Could not query GitHub for user organizations.");
            return -1;
        }

        var orgsponsored = new List<Organization>();
        await status.StartAsync("Querying organization sponsorships", async ctx =>
        {
            // Collect org-sponsored accounts. NOTE: these must be public sponsorships 
            // since the current user would typically NOT be an admin of these orgs.
            foreach (var org in userorgs)
            {
                ctx.Status($"Querying [lime]{org.Login}[/] sponsorships");
                // TODO: we'll need to account for pagination after 100 sponsorships is commonplace :)
                if (await client.QueryAsync(GraphQueries.OrganizationSponsorships(org.Login)) is { Length: > 0 } sponsored)
                {
                    orgsponsored.Add(new Organization(org.Login, sponsored));
                }
            }
        });

        var tree = new Tree(new Markup(":backhand_index_pointing_right:  Sponsoring:"));

        if (usersponsored.Length > 0)
        {
            var user = tree.AddNode(new TreeNode(new Markup($"directly by [yellow]{Account.Login}[/]")));
            var maxlengh = usersponsored.Max(x => x.Sponsorable.Length);
            user.AddNodes(usersponsored.Select(x =>
                new TreeNode(new Markup($"[green]{x.Sponsorable.PadRight(maxlengh)}[/] => ${x.Amount} since {x.CreatedAt:yyyy-MM-dd} {(x.OneTime ? "[dim](one-time)[/]" : "")}"))));
        }

        if (orgsponsored.Count > 0)
        {
            var node = tree.AddNode(new Tree(new Markup("indirectly via [yellow]organizations[/]")));
            foreach (var org in orgsponsored)
            {
                var orgnode = node.AddNode(new TreeNode(new Markup($"[yellow]{org.Login}[/]")));
                orgnode.AddNodes(org.Sponsorables.Select(x => new TreeNode(new Markup($"[green]{x}[/]"))));
            }
        }

        var teamorg = new HashSet<string>();

        if (await client.GetUserContributionsAsync() is { Count: > 0 } contributions)
        {
            var contrib = new Tree(new Markup("indirectly through [yellow]contributions[/]"));
            foreach (var contribution in contributions)
            {
                if (userorgs.Any(x => x.Login == contribution.Key))
                {
                    // If the user belongs to the org, consider this a "team" sponsorship, not contrib. 
                    // This is because the user would typically have contributed to a ton of repos in his org(s).
                    teamorg.Add(contribution.Key);
                }
                else
                {
                    var node = contrib.AddNode(new TreeNode(new Text(contribution.Key, new Style(Color.Green))));
                    node.AddNodes(contribution.Value.Select(x => new TreeNode(new Markup($"[dim]{x}[/]"))));
                }
            }

            if (contrib.Nodes.Count > 0)
                tree.AddNode(contrib);
        }

        if (teamorg.Count > 0)
        {
            var node = tree.AddNode(new Tree(new Markup("indirectly as [yellow]team member[/]")));
            node.AddNodes(teamorg.Select(x => new TreeNode(new Markup($"[green]{x}[/]"))));
        }

        AnsiConsole.Write(tree);

        return 0;
    }
}
