using GraphQuery = (string Query, string? JQ);

namespace Devlooped.Sponsors;

/// <summary>
/// Centralizes queries used to retrieve data from the GitHub GraphQL API.
/// </summary>
static class GraphQL
{
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

    public static GraphQuery UserOrganizations { get; } = (
        """
        query { 
            viewer { 
                organizations(first: 100) {
                    nodes {
                    login
                    isVerified
                    email
                    websiteUrl
                    }
                }
            }
        }
        """,
        """
        [.data.viewer.organizations.nodes.[] | select(.isVerified == true)]
        """);

    public static GraphQuery OrganizationSponsorships { get; } = (
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
        """);

    // Alternative query, but includes private repositories too, not very useful.
    // It's highly unlikely that private repositories will be sponsorable anyway.
    /*
        query {
            viewer {
            contributionsCollection {
                commitContributionsByRepository(maxRepositories: 100) {
                repository {
                    nameWithOwner
                }
                }
            }
            }
        }    
    */

    public static GraphQuery UserContributions { get; } = (
        """
        query {
          viewer {
            repositoriesContributedTo(first: 100, includeUserRepositories: true, contributionTypes: [COMMIT]) {
              nodes {
                nameWithOwner,
                owner {
                  login
                }
              }
            }
          }
        }
        """,
        """
        [.data.viewer.repositoriesContributedTo.nodes.[].owner.login]
        """);
}
