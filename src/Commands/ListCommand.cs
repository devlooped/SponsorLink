using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Lists user and organization sponsorships")]
public class ListCommand(Account account) : Command
{
    record Sponsorship(string Sponsorable, [property: DisplayName("Tier (USD)")] int Dollars,
#if NET6_0_OR_GREATER
        DateOnly CreatedAt,
#else
        DateTime CreatedAt,
#endif
        [property: DisplayName("One-time")] bool OneTime);
    record Organization(string Login, string[] Sponsorables);

    public override int Execute(CommandContext context)
    {
        var status = AnsiConsole.Status();
        string? json = default;
        if (status.Start("Querying user sponsorships", _ =>
            {
                if (!GitHub.TryQuery(
                    """
                    query { 
                      viewer { 
                        sponsorshipsAsSponsor(activeOnly: true, first: 100, orderBy: {field: CREATED_AT, direction: ASC}) {
                          nodes {
                             createdAt
                             isOneTimePayment
                             sponsorable {
                               ... on Organization {
                                 login
                               }
                               ... on User {
                                 login
                               }
                             }
                             tier {
                               monthlyPriceInDollars
                             }        
                          }
                        }
                      }
                    }
                    """,
                    """
                    [.data.viewer.sponsorshipsAsSponsor.nodes.[] | { sponsorable: .sponsorable.login, dollars: .tier.monthlyPriceInDollars, oneTime: .isOneTimePayment, createdAt } ]
                    """, out json) || string.IsNullOrEmpty(json))
                {
                    AnsiConsole.MarkupLine("[red]Could not query GitHub for user sponsorships.[/]");
                    return -1;
                }

                return 0;
            }) == -1)
        {
            return -1;
        }

        var usersponsored = JsonSerializer.Deserialize<Sponsorship[]>(json!, JsonOptions.Default);

        if (status.Start("Querying user organizations", _ =>
            {
                // It's unlikely that any account would belong to more than 100 orgs.
                if (!GitHub.TryQuery(
                    """
                    query { 
                      viewer { 
                        organizations(first: 100) {
                          nodes {
                            login
                          }
                        }
                      }
                    }
                    """,
                    """
                    [.data.viewer.organizations.nodes.[].login]
                    """, out json) || string.IsNullOrEmpty(json))
                {
                    AnsiConsole.MarkupLine("[red]Could not query GitHub for user organizations.[/]");
                    return -1;
                }

                return 0;
            }) == -1)
        {
            return -1;
        }

        var orgs = JsonSerializer.Deserialize<string[]>(json!, JsonOptions.Default) ?? Array.Empty<string>();
        var orgsponsored = new List<Organization>();

        status.Start("Querying organization sponsorships", ctx =>
        {
            // Collect org-sponsored accounts. NOTE: these must be public sponsorships 
            // since the current user would typically NOT be an admin of these orgs.
            foreach (var org in orgs)
            {
                ctx.Status($"Querying {org} sponsorships");
                // TODO: we'll need to account for pagination after 100 sponsorships is commonplace :)
                if (GitHub.TryQuery(
                    $$"""
                    query($login: String!) { 
                      organization(login: $login) { 
                        sponsorshipsAsSponsor(activeOnly: true, first: 100) {
                          nodes {
                             sponsorable {
                               ... on Organization {
                                 login
                               }
                               ... on User {
                                 login
                               }
                             }        
                          }
                        }
                      }
                    }
                    """,
                    """
                    [.data.organization.sponsorshipsAsSponsor.nodes.[].sponsorable.login]
                    """, out json, ("login", org)) &&
                    !string.IsNullOrEmpty(json) &&
                    JsonSerializer.Deserialize<string[]>(json, JsonOptions.Default) is { } sponsored &&
                    sponsored.Length > 0)
                {
                    orgsponsored.Add(new Organization(org, sponsored));
                }
            }
        });

        if (usersponsored != null)
        {
            AnsiConsole.Write(new Paragraph($"Sponsored by {account.Login}", new Style(Color.Green)));
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
