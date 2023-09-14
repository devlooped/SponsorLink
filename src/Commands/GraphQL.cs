namespace Devlooped.Sponsors;

/// <summary>
/// Centralizes queries used to retrieve data from the GitHub GraphQL API.
/// </summary>
static class GraphQL
{
    public const string UserSponsorships =
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
        """;

    public const string UserOrganizations =
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
        """;

    public const string OrganizationSponsorships =
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
        """;

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

    public const string UserContributions =
        """
        query {
          viewer {
            repositoriesContributedTo(first: 100, contributionTypes: [COMMIT]) {
              nodes {
                nameWithOwner
              }
            }
          }
        }
        """;

}
