using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Scriban;

namespace Devlooped.Sponsors;

/// <summary>
/// Represents a GraphQL query and optional JQ filter.
/// </summary>
[DebuggerDisplay("{JQ}")]
public class GraphQuery<T>(string query, string? jq = null)
{
    // Private variable to aid while debugging, to easily copy/paste to the CLI the 
    // various invocation styles for a given query.
    [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
    GraphQueryDebugger CLI => new(query, jq, Variables);

    /// <summary>
    /// The GraphQL query to execute.
    /// </summary>
    public string Query => query;
    /// <summary>
    /// The optional JQ filter to apply to the query result.
    /// </summary>
    public string? JQ => jq;
    /// <summary>
    /// Optional variables used in the query.
    /// </summary>
    public Dictionary<string, object> Variables { get; private set; } = new();

    /// <summary>
    /// Legacy queries use the older REST endpoints rather than the GraphQL API.
    /// </summary>
    public bool IsLegacy { get; set; }

    public override int GetHashCode() => HashCode.Combine(Query, JQ);

    public override bool Equals(object? obj) => obj is GraphQuery<T> other && Query == other.Query && JQ == other.JQ;

    class GraphQueryDebugger
    {
        public GraphQueryDebugger(string query, string? jq, Dictionary<string, object> variables)
        {
            http = variables.Count > 0 ?
            JsonSerializer.Serialize(new
            {
                query,
                variables
            }, JsonOptions.Default) :
            JsonSerializer.Serialize(new { query }, JsonOptions.Default);

            var sb = new StringBuilder();
            sb.Append("gh api graphql");

            foreach (var (name, value) in variables)
                sb.Append($" -F {name}={JsonSerializer.Serialize(value)}");

            sb.Append(" -f query='").Append(query).Append('\'');

            if (jq?.Length > 0)
                sb.Append(" --jq '").Append(jq).Append('\'');

            github = sb.ToString();

            sb.Clear();
            sb.Append("curl -X POST -H \"Authorization: Bearer $(gh auth token)\" -d '");
            sb.Append(http).Append("' https://api.github.com/graphql | convertfrom-json | convertto-json -depth 10");

            if (jq?.Length > 0)
                sb.Append(" | %{ write-host $_; $_ } | jq -r '").Append(jq).Append('\'');

            curl = sb.ToString();
        }

        /// <summary>
        /// Raw HTTP request body.
        /// </summary>
        public string http { get; }
        /// <summary>
        /// GH CLI command.
        /// </summary>
        public string github { get; }
        /// <summary>
        /// PWSH curl + jq command.
        /// </summary>
        public string curl { get; }
    }
}

/// <summary>
/// A query that returns a typed result.
/// </summary>
public class GraphQuery(string query, string? jq = null) : GraphQuery<string>(query, jq)
{
}

/// <summary>
/// Queries used to retrieve data from the GitHub GraphQL API.
/// </summary>
public static class GraphQueries
{
    public static GraphQuery<string[]> ViewerEmails => new(
        "/user/emails",
        "[.[] | select(.verified == true) | .email]")
    {
        IsLegacy = true
    };

    public static GraphQuery<Account> ViewerAccount => new(
        """
        query {
        	viewer {
            login,
            type:__typename
          }
        }
        """,
        """
        .data.viewer
        """);

    public static GraphQuery<Organization[]> ViewerOrganizations { get; } = new(
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

    public static GraphQuery<string[]> ViewerSponsored { get; } = new(
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

    /// <summary>
    /// Gets the tier details for a directly sponsored account by the current user.
    /// </summary>
    /// <param name="account">The account being sponsored.</param>
    /// <remarks>
    /// NOTE: for organization-wide sponsorships, we can't look up what the tier is
    /// (by default? ever?) using the token of a member. Likewise, for contribution-inferred 
    /// "sponsoring" where there's no such thing as a tier. So this is incremental and 
    /// optional information we we'd put on the signed sponsor JWT manifest emitted by the 
    /// sponsorable backend.
    /// </remarks>
    public static GraphQuery<Sponsorship> ViewerSponsorship(string account) => new(
        """
        query($login: String!) {
          viewer {
            sponsorshipsAsSponsor(
              maintainerLogins: [$login]
              activeOnly: true
              first: 1
            ) {
              nodes {
                isOneTimePayment
                tier {
                  description
                  monthlyPriceInDollars
                }
              }
            }
          }
        }
        """,
        """
        .data.viewer.sponsorshipsAsSponsor.nodes[].tier | { tier: .description, amount: .monthlyPriceInDollars }
        """)
    {
        Variables =
        {
            {"login", account }
        }
    };

    /// <summary>
    /// Returns the unique repository owners of all repositories the user has contributed 
    /// commits to.
    /// </summary>
    /// <remarks>
    /// If a single user contributes to more than 100 repositories, we'd have a problem 
    /// and would need to implement pagination.
    /// </remarks>
    public static GraphQuery<string[]> ViewerContributions { get; } = new(
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

    /// <summary>
    /// Gets the login of the active user plus the organizations he belongs to. He can 
    /// be considered a sponsor of all those organizations by belonging to them if they 
    /// implement SponsorLink.
    /// </summary>
    public static GraphQuery<string[]> ViewerSponsorableCandidates => new(
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

    public static GraphQuery<string[]> OrganizationSponsorships(string organization) => new(
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
        """)
    {
        Variables =
        {
            {"login", organization }
        }
    };

    public static GraphQuery<string[]> UserSponsorships(string user) => new(
        $$"""
        query($login: String!) { 
            user(login: $login) { 
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
        """)
    {
        Variables =
        {
            {"login", user }
        }
    };

    public static GraphQuery<Organization[]> UserOrganizations(string user) => new(
        """
        query($login: String!) { 
            user(login: $login) { 
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
        [.data.user.organizations.nodes.[] | select(.isVerified == true)]
        """)
    {
        Variables =
        {
            { "login", user }
        }
    };

    /// <summary>
    /// Returns the unique repository owners of all repositories the user has contributed 
    /// commits to.
    /// </summary>
    /// <remarks>
    /// If a single user contributes to more than 100 repositories, we'd have a problem 
    /// and would need to implement pagination.
    /// </remarks>
    public static GraphQuery<string[]> UserContributions(string user) => new(
        """
        query($login: String!) {
            user(login: $login) {
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
        [.data.user.repositoriesContributedTo.nodes.[].owner.login | unique]
        """)
    {
        Variables =
        {
            { "login", user }
        }
    };

    /// <summary>
    /// Gets the login of the active user plus the organizations he belongs to. He can 
    /// be considered a sponsor of all those organizations by belonging to them if they 
    /// implement SponsorLink.
    /// </summary>
    public static GraphQuery<string[]> UserSponsorableCandidates(string user) => new(
        """
        query($login: String!) {
            user(login: $login) {
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
        [.data.user.login] + [.data.user.organizations.nodes[].login]
        """)
    {
        Variables =
        {
            { "login", user }
        }
    };


    /// <summary>
    /// Gets the account login and type that owns the [account]/.github repository.
    /// </summary>
    /// <param name="account">The account to look up, which must own a repository named <c>.github</c>.</param>
    /// <returns></returns>
    public static GraphQuery<Account> Sponsorable(string account) => new(
        """
        query($login: String!) {
            repository(owner: $login, name: ".github") {
                owner {
                    login
                    type: __typename
                }
            }
        }
        """,
        """
        .data.repository.owner
        """)
    {
        Variables =
        {
            { "login", account }
        }
    };

    /// <summary>
    /// Tries to get a user account.
    /// </summary>
    public static GraphQuery<Account> FindUser(string account) => new(
        """
        query($login: String!) {
          user(login: $login) {
            login
        	type: __typename
          }
        }
        """,
        """
        .data.user
        """)
    {
        Variables =
        {
            { "login", account }
        }
    };

    /// <summary>
    /// Tries to get an organization account.
    /// </summary>
    public static GraphQuery<Account> FindOrganization(string account) => new(
        """
        query($login: String!) {
          organization(login: $login) {
            login
        	type: __typename
          }
        }
        """,
        """
        .data.organization
        """)
    {
        Variables =
        {
            { "login", account }
        }
    };

    public static GraphQuery<string[]> IsSponsoredBy(string account, IEnumerable<string> candidates) => new(
        // NOTE: we replace the '-' char which would be invalid as a return field with '___'
        Template.Parse(
            """
            query($login: String!) { 
                user(login: $login) {
                    {{ for candidate in candidates }}
                    {{ candidate | string.replace "-" "___" }}: isSponsoredBy(accountLogin:"{{ candidate }}")
                    {{ end }}
                },
                organization(login: $login) {
                    {{ for candidate in candidates }}
                    {{ candidate | string.replace "-" "___" }}: isSponsoredBy(accountLogin:"{{ candidate }}")
                    {{ end }}
                }
            }
            """).Render(new { candidates }),
        // At projection time, we replace back the ids to '-' from '___'
        """
        [(.data.user? + .data.organization?) | to_entries[] |select(.value == true) | .key | gsub("___"; "-")] | unique
        """)
    {
        Variables =
        {
            { "login", account }
        }
    };

    public static GraphQuery<string[]> IsSponsoredBy(string account, params string[] candidates)
        => IsSponsoredBy(account, (IEnumerable<string>)candidates);

    public static GraphQuery<Organization[]> VerifiedSponsoringOrganizations(string account) => new(
        """
        query ($owner: String!, $endCursor: String) {
          user(login: $owner) {
            sponsorshipsAsMaintainer(first: 100, after: $endCursor) {
              nodes {
                sponsorEntity {
                  ... on Organization {
                    email
                    isVerified
                    url
                    websiteUrl
                  }
                }
              }
              pageInfo {
                hasNextPage
                endCursor
              }
            }
          }
          organization(login: $owner) {
            sponsorshipsAsMaintainer(first: 100, after: $endCursor) {
              nodes {
                sponsorEntity {
                  ... on Organization {
                    email
                    isVerified
                    url
                    websiteUrl
                  }
                }
              }
              pageInfo {
                hasNextPage
                endCursor
              }
            }
          }
        }
        """,
        """
        [(.data.user? + .data.organization?).sponsorshipsAsMaintainer.nodes.[].sponsorEntity | select(.isVerified == true)]
        """
        )
    {
        Variables =
        {
            { "owner", account }
        }
    };
}