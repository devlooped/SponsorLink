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

    public static GraphQuery ViewerSponsorships { get; } = (
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

    public static GraphQuery ViewerContributions { get; } = (
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

    public static GraphQuery OrganizationSponsorships(string account) => (
        $$"""
        query { 
            organization(login: $account$) { 
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
        """.Replace("$account$", account),
        """
        [.data.organization.sponsorshipsAsSponsor.nodes.[].sponsorable.login]
        """);

    public static GraphQuery ViewerOrganizations { get; } = (
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
}

