using Scriban;

namespace Devlooped.Sponsors;

/// <summary>
/// Queries used to retrieve data from the GitHub GraphQL API.
/// </summary>
public static class GraphQueries
{
    public static GraphQuery ViewerLogin => new(
        """
        query { viewer { login } }
        """,
        """
        .data.viewer.login
        """);

    public static GraphQuery ViewerOrganizations { get; } = new(
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
        UserOrganizations("viewer").JQ);

    public static GraphQuery ViewerSponsored { get; } = new(
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

    public static GraphQuery ViewerContributions { get; } = new(
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
        [.data.viewer.repositoriesContributedTo.nodes.[].owner.login] | unique
        """);

    public static GraphQuery OrganizationSponsorships(string organization) => new(
        $$"""
        query { 
            organization(login: "$login$") { 
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
        """.Replace("$login$", organization, StringComparison.Ordinal),
        """
        [.data.organization.sponsorshipsAsSponsor.nodes.[].sponsorable.login]
        """);

    public static GraphQuery UserOrganizations(string user) => new(
        """
        query { 
            user(login: "$login$" { 
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
        """.Replace("$login$", user, StringComparison.Ordinal),
        """
        [.data.viewer.organizations.nodes.[] | select(.isVerified == true)]
        """);


    /// <summary>
    /// Gets the login of the active user plus the organizations he belongs to. He can 
    /// be considered a sponsor of all those organizations by belonging to them if they 
    /// implement SponsorLink.
    /// </summary>
    public static GraphQuery ViewerSponsorableCandidates => new(
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

    /// <summary>
    /// Gets the account login and type that owns the [account]/.github repository.
    /// </summary>
    /// <param name="account">The account to look up, which must own a repository named <c>.github</c>.</param>
    /// <returns></returns>
    public static GraphQuery Sponsorable(string account) => new(
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

    /// <summary>
    /// Tries to get a user account.
    /// </summary>
    public static GraphQuery FindUser(string account) => new(
        """
        query {
          user(login:"$account$") {
            login
        	type: __typename
          }
        }
        """.Replace("$account$", account),
        """
        .data.user
        """
        );

    /// <summary>
    /// Tries to get an organization account.
    /// </summary>
    public static GraphQuery FindOrganization(string account) => new(
        """
        query {
          organization(login:"$account$") {
            login
        	type: __typename
          }
        }
        """.Replace("$account$", account),
        """
        .data.organization
        """
        );

    public static GraphQuery IsSponsoredBy(string account, AccountType type, params string[] candidates) => new(
        // NOTE: we replace the '-' char which would be invalid as a return field with '___'
        Template.Parse(
            """
            query { 
                {{ type }}(login: "{{ account }}") {
                    {{ for login in candidates }}
                    {{ login | string.replace "-" "___" }}: isSponsoredBy(accountLogin:"{{ login }}")
                    {{ end }}
                }
            }
            """).Render(new { account, type = type.ToString().ToLowerInvariant(), candidates }),
        // At projection time, we replace back the ids to '-' from '___'
        """
        [(.data.[] | to_entries[] | select(.value == true) | .key | gsub("___"; "-"))]
        """);
}

