using Scriban;

namespace Devlooped.Sponsors;

/// <summary>
/// Queries used to retrieve data from the GitHub GraphQL API.
/// </summary>
public static class GraphQueries
{
    /// <summary>
    /// Viewer emails are not available in GraphQL (yet?). So we must use a 
    /// legacy query via REST API.
    /// </summary>
    /// <remarks>
    /// See https://github.com/orgs/community/discussions/24389#discussioncomment-3243994
    /// </remarks>
    public static GraphQuery<string[]> ViewerEmails => new(
        "/user/emails",
        // see https://stackoverflow.com/a/2387072/24684 on how this works to exclude noreply addresses
        """
        [.[] | select(.verified == true) | select(.email | test("^((?!noreply.github.com).)*$")) | .email]
        """)
    {
        IsLegacy = true
    };

    /// <summary>
    /// Returns a tuple of (login, type) for the viewer.
    /// </summary>
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

    /// <summary>
    /// Gets the organizations the viewer belongs to, filtering out 
    /// those entries without a verified email or websiteUrl.
    /// </summary>
    /// <remarks>
    /// NOTE: we only get the first 100 organizations.
    /// </remarks>
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

    /// <summary>
    /// Gets the sponsorable logins that the viewer is sponsoring.
    /// </summary>
    public static GraphQuery<string[]> ViewerSponsored { get; } = CoreViewerSponsored();

    /// <summary>
    /// Gets the account logins that the viewer is sponsoring.
    /// </summary>
    internal static GraphQuery<string[]> CoreViewerSponsored(int pageSize = 100) => new(
        """
        query($pageSize: Int!, $endCursor: String) { 
            viewer { 
                sponsorshipsAsSponsor(activeOnly: true, first: $pageSize, orderBy: {field: CREATED_AT, direction: ASC}, after: $endCursor) {
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
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                }
            }
        }
        """,
        """
        [.data.viewer.sponsorshipsAsSponsor.nodes.[].sponsorable.login]
        """)
    {
        Variables =
        {
            { "pageSize", pageSize }
        }
    };

    /// <summary>
    /// Gets the tier details for a directly sponsored account by the current user.
    /// </summary>
    /// <param name="sponsorable">The account being sponsored.</param>
    /// <remarks>
    /// NOTE: for organization-wide sponsorships, we can't look up what the tier is
    /// (by default? ever?) using the token of a member. Likewise, for contribution-inferred 
    /// "sponsoring" where there's no such thing as a tier. So this is incremental and 
    /// optional information we we'd put on the signed sponsor JWT manifest emitted by the 
    /// sponsorable backend.
    /// </remarks>
    public static GraphQuery<Sponsorship> ViewerSponsorship(string sponsorable) => new(
        """
        query($account: String!) {
          viewer {
            sponsorshipsAsSponsor(
              maintainerLogins: [$account]
              activeOnly: true
              first: 1
            ) {
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
                  description
                  monthlyPriceInDollars
                }
              }
            }
          }
        }
        """,
        """
        .data.viewer.sponsorshipsAsSponsor.nodes.[] | { sponsorable: .sponsorable.login, tier: .tier.description, amount: .tier.monthlyPriceInDollars, oneTime: .isOneTimePayment, createdAt }
        """)
    {
        Variables =
        {
            { "account", sponsorable }
        }
    };

    /// <summary>
    /// Gets the tier details for all directly sponsored accounts by the current user.
    /// </summary>
    /// <remarks>
    /// NOTE: for organization-wide sponsorships, we can't look up what the tier is
    /// (by default? ever?) using the token of a member. Likewise, for contribution-inferred 
    /// "sponsoring" where there's no such thing as a tier. So this is incremental and 
    /// optional information we we'd put on the signed sponsor JWT manifest emitted by the 
    /// sponsorable backend.
    /// </remarks>
    public static GraphQuery<Sponsorship[]> ViewerSponsorships { get; } = CoreViewerSponsorships();

    /// <summary>
    /// Gets the tier details for all directly sponsored accounts by the current user.
    /// </summary>
    /// <remarks>
    /// NOTE: for organization-wide sponsorships, we can't look up what the tier is
    /// (by default? ever?) using the token of a member. Likewise, for contribution-inferred 
    /// "sponsoring" where there's no such thing as a tier. So this is incremental and 
    /// optional information we we'd put on the signed sponsor JWT manifest emitted by the 
    /// sponsorable backend.
    /// </remarks>
    public static GraphQuery<Sponsorship[]> CoreViewerSponsorships(int pageSize = 100) => new(
        """
        query($pageSize: Int!, $endCursor: String) {
          viewer {
            sponsorshipsAsSponsor(activeOnly: true, first: $pageSize, after: $endCursor) {
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
                  description
                  monthlyPriceInDollars
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
        [.data.viewer.sponsorshipsAsSponsor.nodes.[] | { sponsorable: .sponsorable.login, tier: .tier.description, amount: .tier.monthlyPriceInDollars, oneTime: .isOneTimePayment, createdAt }]
        """)
    {
        Variables =
        {
            { "pageSize", pageSize }
        }
    };

    /// <summary>
    /// Returns the unique repository owners of all repositories the user has contributed 
    /// commits to.
    /// </summary>
    public static GraphQuery<string[]> ViewerContributedRepoOwners { get; } = CoreViewerContributedRepoOwners();

    /// <summary>
    /// Returns the unique repository owners of all repositories the user has contributed 
    /// commits to.
    /// </summary>
    internal static GraphQuery<string[]> CoreViewerContributedRepoOwners(int pageSize = 100) => new(
        """
        query($pageSize: Int!, $endCursor: String) {
            viewer {
                repositoriesContributedTo(first: $pageSize, includeUserRepositories: true, contributionTypes: [COMMIT], after: $endCursor) {
                    nodes {
                        owner {
                            login
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
        [.data.viewer.repositoriesContributedTo.nodes.[].owner.login] | unique
        """)
    {
        Variables =
        {
            { "pageSize", pageSize }
        }
    };

    /// <summary>
    /// Returns the unique repository name+owner of all repositories the user has contributed 
    /// commits to.
    /// </summary>
    public static GraphQuery<string[]> ViewerContributedRepositories { get; } = CoreViewerContributedRepositories();

    /// <summary>
    /// Returns the unique repository name+owner of all repositories the user has contributed 
    /// commits to.
    /// </summary>
    internal static GraphQuery<string[]> CoreViewerContributedRepositories(int pageSize = 100) => new(
        """
        query($pageSize: Int!, $endCursor: String) {
            viewer {
                repositoriesContributedTo(first: $pageSize, includeUserRepositories: true, contributionTypes: [COMMIT], after: $endCursor) {
                    nodes {
                        nameWithOwner,
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
        [.data.viewer.repositoriesContributedTo.nodes.[].nameWithOwner] | unique
        """)
    {
        Variables =
        {
            { "pageSize", pageSize }
        }
    };

    /// <summary>
    /// Gets the login of the active user plus the organizations he belongs to. He can 
    /// be considered a sponsor of all those organizations by belonging to them if they 
    /// implement SponsorLink.
    /// </summary>
    public static GraphQuery<string[]> ViewerSponsorableCandidates => new(
        // NOTE: we don't paginate here since it's unlikely a user would belong to more than 100 orgs.
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
    /// Gets the account logins that the given organization is actively sponsoring.
    /// </summary>
    public static GraphQuery<string[]> OrganizationSponsorships(string organization, int pageSize = 100) => new(
        $$"""
        query($login: String!, $pageSize: Int!, $endCursor: String) { 
            organization(login: $login) { 
                sponsorshipsAsSponsor(activeOnly: true, first: $pageSize, after: $endCursor) {
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
                    pageInfo {
                        hasNextPage
                        endCursor
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
            { "login", organization },
            { "pageSize", pageSize }
        }
    };

    public static GraphQuery<string[]> UserSponsorships(string user, int pageSize = 100) => new(
        $$"""
        query($login: String!, $pageSize: Int!, $endCursor: String) { 
            user(login: $login) { 
                sponsorshipsAsSponsor(activeOnly: true, first: $pageSize, after: $endCursor) {
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
                    pageInfo {
                        hasNextPage
                        endCursor
                    }
                }
            }
        }
        """,
        """
        [.data.user.sponsorshipsAsSponsor.nodes.[].sponsorable.login]
        """)
    {
        Variables =
        {
            {"login", user },
            { "pageSize", pageSize }
        }
    };

    public static GraphQuery<Organization[]> UserOrganizations(string user, int pageSize = 100) => new(
        """
        query($login: String!, $pageSize: Int!, $endCursor: String) { 
            user(login: $login) { 
                organizations(first: $pageSize, after: $endCursor) {
                    nodes {
                        login
                        isVerified
                        email
                        websiteUrl
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
        [.data.user.organizations.nodes.[] | select(.isVerified == true)]
        """)
    {
        Variables =
        {
            {"login", user },
            { "pageSize", pageSize }
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
    public static GraphQuery<string[]> UserContributions(string user, int pageSize = 100) => new(
        """
        query($login: String!, $endCursor: String, $count: Int!) {
            user(login: $login) {
                repositoriesContributedTo(first: $count, includeUserRepositories: true, contributionTypes: [COMMIT], after: $endCursor) {
                    nodes {
                        nameWithOwner,
                        owner {
                            login
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
        [.data.user.repositoriesContributedTo.nodes.[].owner.login] | unique
        """)
    {
        Variables =
        {
            { "login", user },
            { "count", pageSize }
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

    /// <summary>
    /// Gets a repository's default branch, if it exists.
    /// </summary>
    public static GraphQuery<string?> DefaultBranch(string owner, string repo) => new(
        """
        query($owner: String!, $repo: String!) {
          repository(owner: $owner, name: $repo) {
            defaultBranchRef {
              name
            }
          }
        }
        """,
        """
        .data.repository?.defaultBranchRef.name
        """)
    {
        Variables =
        {
            { nameof(owner), owner },
            { nameof(repo), repo },
        }
    };

    public static GraphQuery<string[]> IsSponsoredBy(string sponsorable, IEnumerable<string> candidateSponsors) => new(
        // NOTE: we replace the '-' char which would be invalid as a return field with '___'
        Template.Parse(
            """
            query($login: String!) { 
              user(login: $login) {
                {{~ for candidate in candidates ~}}
                {{ candidate | string.replace "-" "___" }}: isSponsoredBy(accountLogin:"{{ candidate }}")
                {{~ end ~}}
              },
              organization(login: $login) {
                {{~ for candidate in candidates ~}}
                {{ candidate | string.replace "-" "___" }}: isSponsoredBy(accountLogin:"{{ candidate }}")
                {{~ end ~}}
              }
            }
            """).Render(new { candidates = candidateSponsors }),
        // At projection time, we replace back the ids to '-' from '___'
        """
        [(.data.user? + .data.organization?) | to_entries[] |select(.value == true) | .key | gsub("___"; "-")] | unique
        """)
    {
        Variables =
        {
            { "login", sponsorable }
        }
    };

    public static GraphQuery<string[]> IsSponsoredBy(string sponsorable, params string[] candidateSponsors)
        => IsSponsoredBy(sponsorable, (IEnumerable<string>)candidateSponsors);

    public static GraphQuery Tiers(string account) => new(
        """
        query ($login: String!) {
          user(login: $login) {
            sponsorsListing {
              tiers(first: 100){
                nodes {
                  name,
                  description,
                  monthlyPriceInDollars,
                  isOneTime,
                  closestLesserValueTier {
                    name
                  },
                }
              }
            }
          }
          organization(login: $login) {
            sponsorsListing {
              tiers(first: 100){
                nodes {
                  name,
                  description,
                  monthlyPriceInDollars,
                  isOneTime,
                  closestLesserValueTier {
                    name
                  },
                }
              }
            }
          }
        }
        """,
        """
        [(.data.user? + .data.organization?).sponsorsListing.tiers.nodes.[] | { name, description, amount: .monthlyPriceInDollars, oneTime: .isOneTime, previous: .closestLesserValueTier.name }]
        """)
    {
        Variables =
        {
            { "login", account }
        }
    };

    /// <summary>
    /// Gets the verified sponsoring organizations for a given sponsorable organization.
    /// </summary>
    public static GraphQuery<Organization[]> SponsoringOrganizationsForOrg(string sponsorableOrganization, int pageSize = 100) => new(
        """
        query ($account: String!, $pageSize: Int!, $endCursor: String) {
          organization(login: $account) {
            sponsorshipsAsMaintainer(first: $pageSize, after: $endCursor) {
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
        [.data.organization.sponsorshipsAsMaintainer.nodes.[].sponsorEntity | select(.isVerified == true)]
        """
        )
    {
        Variables =
        {
            { "account", sponsorableOrganization },
            { "pageSize", pageSize }
        }
    };
    /// <summary>
    /// Gets the verified sponsoring organizations for a given sponsorable user.
    /// </summary>
    public static GraphQuery<Organization[]> SponsoringOrganizationsForUser(string sponsorableUser, int pageSize = 100) => new(
        """
        query ($account: String!, $endCursor: String) {
          user(login: $account) {
            sponsorshipsAsMaintainer(first: $pageSize, after: $endCursor) {
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
        [.data.user.sponsorshipsAsMaintainer.nodes.[].sponsorEntity | select(.isVerified == true)]
        """
        )
    {
        Variables =
        {
            { "account", sponsorableUser },
            { "pageSize", pageSize }
        }
    };
}