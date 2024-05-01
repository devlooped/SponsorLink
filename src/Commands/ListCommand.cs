using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Lists user and organization sponsorships")]
public class ListCommand(ICommandApp app, IGraphQueryClient client) : GitHubAsyncCommand(app)
{
    record Organization(string Login, string[] Sponsorables);

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var result = await base.ExecuteAsync(context);
        if (result != 0)
            return result;

        Debug.Assert(Account != null, "After authentication, Account should never be null.");

        var status = AnsiConsole.Status();

        var usersponsored = await status.StartAsync("Querying user sponsorships", _ => client.QueryAsync(GraphQueries.ViewerSponsorships));
        if (usersponsored == null)
        {
            AnsiConsole.MarkupLine("[red]Could not query GitHub for user sponsorships.[/]");
            return -1;
        }

        var userorgs = await status.StartAsync("Querying user organizations", _ => client.QueryAsync(GraphQueries.ViewerOrganizations));
        if (userorgs == null)
        {
            AnsiConsole.MarkupLine("[red]Could not query GitHub for user organizations.[/]");
            return -1;
        }

        var orgsponsored = new List<Organization>();
        await status.StartAsync("Querying organization sponsorships", async ctx =>
        {
            // Collect org-sponsored accounts. NOTE: these must be public sponsorships 
            // since the current user would typically NOT be an admin of these orgs.
            foreach (var org in userorgs)
            {
                ctx.Status($"Querying {org.Login} sponsorships");
                // TODO: we'll need to account for pagination after 100 sponsorships is commonplace :)
                if (await client.QueryAsync(GraphQueries.OrganizationSponsorships(org.Login)) is { Length: > 0 } sponsored)
                {
                    orgsponsored.Add(new Organization(org.Login, sponsored));
                }
            }
        });

        if (usersponsored != null)
        {
            AnsiConsole.Write(new Paragraph($"Sponsored by {Account.Login}", new Style(Color.Green)));
            AnsiConsole.WriteLine();
            AnsiConsole.Write(usersponsored.AsTable());
        }

        if (orgsponsored.Count > 0)
        {
            var tree = new Tree(new Text("Sponsored by Organizations", new Style(Color.Yellow)));

            foreach (var org in orgsponsored)
            {
                var node = new TreeNode(new Text(org.Login, new Style(Color.Green)));
                node.AddNodes(org.Sponsorables);
                tree.AddNode(node);
            }

            AnsiConsole.Write(tree);
        }

        return 0;
    }
}
