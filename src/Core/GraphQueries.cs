using System.Text;
using Scriban;
using GraphQuery = (string Query, string? JQ);

namespace Devlooped.Sponsors;

/// <summary>
/// Queries used to retrieve data from the GitHub GraphQL API.
/// </summary>
public static class GraphQueries
{
    public static GraphQuery ViewerLogin => (
        """
        query { viewer { login } }
        """,
        """
        .data.viewer.login
        """);

    public static GraphQuery Sponsorable(string account) => (
        """
        query {
          repository(owner: "$account$", name: ".github") {
            owner {
              login
              type: __typename
            }
          }
        }
        """.Replace("$account$", account),
        """
        .data.repository.owner
        """
        );

    public static GraphQuery IsSponsoredBy(string account, AccountType type, params string[] candidates) => (
        Template.Parse(
            """
            query { 
                {{ type }}(login: "{{ account }}") {
                    {{ for login in candidates }}
                    {{ login }}: isSponsoredBy(accountLogin:"{{ login }}")
                    {{ end }}
                }
            }
            """).Render(new { account, type = type.ToString().ToLowerInvariant(), candidates }),
        """
        [(.data.[] | to_entries[] | select(.value == true) | .key)]
        """);

    public static GraphQuery UserSponsorCandidates => (
        """
        query {
          viewer {
            login
            organizations(first:100) {
              nodes {
                login
              }
            }
          }
        }
        """,
        """
        [.data.viewer.login] + [.data.viewer.organizations.nodes[].login]
        """);

    public static GraphQuery UserSponsorships { get; } = (
        """
        query { 
          viewer { 
            sponsorshipsAsSponsor(activeOnly: true, first: 100, orderBy: {field: CREATED_AT, direction: ASC}) {
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
        [.data.viewer.sponsorshipsAsSponsor.nodes.[].sponsorable.login]
        """);

    public static GraphQuery UserContributions { get; } = (
        """
        query {
          viewer {
            repositoriesContributedTo(first: 100, includeUserRepositories: true, contributionTypes: [COMMIT]) {
              nodes {
                owner {
                  login
                }
              }
            }
          }
        }
        """,
        """
        [.data.viewer.repositoriesContributedTo.nodes.[].owner.login] | unique
        """);
}

