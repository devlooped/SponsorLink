using Devlooped.Sponsors;
using DotNetConfig;
using Microsoft.Extensions.DependencyInjection;
using static Devlooped.Helpers;
using static Devlooped.Sponsors.Process;

namespace Devlooped.Tests;

public class GraphQueriesTests(ITestOutputHelper output)
{
    [SecretsFact("GitHub:Token")]

    public async Task GetDefaultBranch()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var branch = await client.QueryAsync(GraphQueries.DefaultBranch("devlooped", ".github"));

        Assert.NotNull(branch);
        Assert.Equal("main", branch);
    }

    [SecretsFact("GitHub:Token")]

    public async Task GetDefaultBranchNonExistentReturnsNull()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var branch = await client.QueryAsync(GraphQueries.DefaultBranch("devlooped", "githubs"));

        Assert.Null(branch);
    }

    [LocalFact]

    public async Task GetDefaultBranchNonExistentReturnsNullCli()
    {
        var client = new CliGraphQueryClient();

        var branch = await client.QueryAsync(GraphQueries.DefaultBranch("devlooped", "githubs"));

        Assert.Null(branch);
    }

    [SecretsFact("GitHub:Token")]

    public async Task GetFundingHttp()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var sponsorables = await client.QueryAsync(GraphQueries.Funding(["PrismLibrary/Prism", "curl/curl", "devlooped/moq"]));

        Assert.NotNull(sponsorables);
        Assert.NotEmpty(sponsorables);
        Assert.Contains(sponsorables, x => x.OwnerRepo == "devlooped/moq" && x.Sponsorables.Contains("devlooped"));
    }

    [LocalFact]

    public async Task GetFundingCli()
    {
        var client = new CliGraphQueryClient();

        var sponsorables = await client.QueryAsync(GraphQueries.Funding(["PrismLibrary/Prism", "curl/curl", "devlooped/moq"]));

        Assert.NotNull(sponsorables);
        Assert.NotEmpty(sponsorables);
        Assert.Contains(sponsorables, x => x.OwnerRepo == "devlooped/moq" && x.Sponsorables.Contains("devlooped"));
    }

    [SecretsFact("GitHub:Token")]
    public async Task CanConvertPrimitiveTypesHttp() => await ConvertPrimitiveTypes(new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub"));

    [LocalFact]
    public async Task CanConvertPrimitiveTypesCli() => await ConvertPrimitiveTypes(new CliGraphQueryClient());

    static async Task ConvertPrimitiveTypes(IGraphQueryClient client)
    {
        var value = await client.QueryAsync<int?>(
            """
            query {
              repository(owner:"devlooped", name:"moq") {
            	stargazerCount
              }
            }
            """,
            """
            .data.repository.stargazerCount
            """);

        Assert.NotNull(value);
        Assert.True(value > 0);

        var value2 = await client.QueryAsync<bool?>(
            """
            query {
              repository(owner:"devlooped", name:"moq") {
                isTemplate
              }
            }
            """,
            """
            .data.repository.isTemplate
            """);

        Assert.NotNull(value2);
        Assert.False(value2);

        var value3 = await client.QueryAsync<DateTime?>(
            """
            query {
              repository(owner:"devlooped", name:"moq") {
                createdAt
              }
            }
            """,
            """
            .data.repository.createdAt
            """);

        Assert.NotNull(value3);
        Assert.True(value3 < DateTime.UtcNow);

        var value4 = await client.QueryAsync<DateTimeOffset?>(
            """
            query {
              repository(owner:"devlooped", name:"moq") {
                createdAt
              }
            }
            """,
            """
            .data.repository.createdAt
            """);

        Assert.NotNull(value4);
        Assert.True(value4 < DateTimeOffset.UtcNow);

    }

    [SecretsFact("GitHub:Token")]
    public async Task GetOrganization()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var account = await client.QueryAsync<Account>(GraphQueries.FindOrganization("dotnet"));

        Assert.NotNull(account);
        Assert.Equal(AccountType.Organization, account.Type);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetUser()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var account = await client.QueryAsync<Account>(GraphQueries.FindUser("davidfowl"));

        Assert.NotNull(account);
        Assert.Equal(AccountType.User, account.Type);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetDotGitHubSponsorableOrganization()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var account = await client.QueryAsync<Account>(GraphQueries.Sponsorable("dotnet"));

        Assert.NotNull(account);
        Assert.Equal(AccountType.Organization, account.Type);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetDotGitHubSponsorableUser()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var account = await client.QueryAsync<Account>(GraphQueries.Sponsorable("xoofx"));

        Assert.NotNull(account);
        Assert.Equal(AccountType.User, account.Type);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetNonExistent()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        // davidfowl does not have a .github repo as of now
        Assert.Null(await client.QueryAsync<Account>(GraphQueries.Sponsorable("davidfowl")));

        Assert.Null(await client.QueryAsync<Account>(GraphQueries.FindUser(Guid.NewGuid().ToString("N"))));
        Assert.Null(await client.QueryAsync<Account>(GraphQueries.FindOrganization(Guid.NewGuid().ToString("N"))));
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetViewerLogin()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var account = await client.QueryAsync(GraphQueries.ViewerAccount);

        Assert.NotNull(account);
        Assert.NotEmpty(account.Login);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetViewerSponsorships()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var logins = await client.QueryAsync<string[]>(GraphQueries.ViewerSponsored);

        // Will only succeed if the GitHub token in use belongs to a user with sponsorships.
        Assert.NotNull(logins);
        Assert.NotEmpty(logins);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetViewerSponsorableCandidates()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var candidates = await client.QueryAsync(GraphQueries.ViewerSponsorableCandidates);
        var viewer = await client.QueryAsync(GraphQueries.ViewerAccount);
        var orgs = await client.QueryAsync<Organization[]>(GraphQueries.ViewerOrganizations);

        Assert.NotNull(candidates);
        Assert.NotNull(viewer);
        Assert.NotNull(orgs);

        Assert.Contains(viewer.Login, candidates);
        Assert.All(orgs, org => Assert.Contains(org.Login, candidates));
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetUserOrganizations()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var orgs = await client.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("davidfowl"));

        Assert.NotNull(orgs);
        Assert.True(orgs.Length > 2);
        Assert.Contains(orgs, x => x.Login == "dotnet");
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetUserOrganizations2()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var orgs = await client.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("jamesls"));

        Assert.NotNull(orgs);
        Assert.Contains(orgs, x => x.Login == "aws");
    }

    [SecretsFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetHttpSponsorships()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");
        var sponsorable = Helpers.Configuration["SponsorLink:Account"];

        Assert.NotNull(sponsorable);

        var account =
            await client.QueryAsync(GraphQueries.FindUser(sponsorable)) ??
            await client.QueryAsync(GraphQueries.FindOrganization(sponsorable));

        Assert.NotNull(account);

        var sponsorships = await GetSponsoringOrganizations(client, account, sponsorable);

        Assert.NotNull(sponsorships);
        Assert.NotEmpty(sponsorships);

        // NOTE: this would only pass if you have at least 2 sponsoring (verified) orgs. 
        Assert.True(sponsorships.Length > 2);
    }

    [LocalFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetCliSponsorships()
    {
        EnsureAuthenticated();
        var client = new CliGraphQueryClient();
        var sponsorable = Helpers.Configuration["SponsorLink:Account"];

        if (string.IsNullOrEmpty(sponsorable))
            return;

        var account =
            await client.QueryAsync(GraphQueries.FindUser(sponsorable)) ??
            await client.QueryAsync(GraphQueries.FindOrganization(sponsorable));

        Assert.NotNull(account);

        var sponsorships = await GetSponsoringOrganizations(client, account, sponsorable);

        Assert.NotNull(sponsorships);
        Assert.NotEmpty(sponsorships);

        // NOTE: this would only pass if you have at least 2 sponsoring (verified) orgs. 
        Assert.True(sponsorships.Length > 2);
    }

    [LocalFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetPagedSponsorships()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");
        var sponsorable = Helpers.Configuration["SponsorLink:Account"];

        if (string.IsNullOrEmpty(sponsorable))
            return;

        var account =
            await cli.QueryAsync(GraphQueries.FindUser(sponsorable)) ??
            await cli.QueryAsync(GraphQueries.FindOrganization(sponsorable));

        Assert.NotNull(account);

        var clidata = await GetSponsoringOrganizations(cli, account, sponsorable, 2);
        var httpdata = await GetSponsoringOrganizations(http, account, sponsorable, 2);

        Assert.NotNull(clidata);
        Assert.NotNull(httpdata);
        Assert.Equal(clidata, httpdata);

        // NOTE: this would only pass if you have at least 2 sponsoring (verified) orgs. 
        Assert.True(httpdata.Length > 2);
    }

    static async Task<Organization[]?> GetSponsoringOrganizations(IGraphQueryClient client, Account account, string sponsorable, int pageSize = 100)
    {
        var sponsorships = account.Type == Sponsors.AccountType.User ?
            await client.QueryAsync(GraphQueries.SponsoringOrganizationsForUser(sponsorable, pageSize)) :
            await client.QueryAsync(GraphQueries.SponsoringOrganizationsForOrg(sponsorable, pageSize));

        Assert.NotNull(sponsorships);
        Assert.NotEmpty(sponsorships);

        return sponsorships;
    }


    [LocalFact("GitHub:Token")]
    public async Task GetContributors()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync(GraphQueries.RepositoryContributors("devlooped", "ThisAssembly"));
        var httpdata = await http.QueryAsync(GraphQueries.RepositoryContributors("devlooped", "ThisAssembly"));

        Assert.NotNull(clidata);
        Assert.NotNull(httpdata);
        Assert.Equal(clidata, httpdata);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedContributors()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync(GraphQueries.RepositoryContributors("dotnet", "aspnetcore"));

        Assert.NotNull(clidata);
        // We can properly paginate.
        Assert.True(clidata.Length > 200);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedViewerSponsored()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync(GraphQueries.CoreViewerSponsored(2));
        var httpdata = await http.QueryAsync(GraphQueries.CoreViewerSponsored(2));

        Assert.NotNull(clidata);
        Assert.NotNull(httpdata);
        Assert.Equal(clidata, httpdata);

        // NOTE: this would only pass if you have at least 2 sponsorships
        Assert.True(httpdata.Length > 2);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedViewerSponsorships()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync(GraphQueries.CoreViewerSponsorships(2));
        var httpdata = await http.QueryAsync(GraphQueries.CoreViewerSponsorships(2));

        Assert.NotNull(clidata);
        Assert.NotNull(httpdata);
        Assert.Equal(clidata, httpdata);

        // NOTE: this would only pass if you have at least 2 sponsorships
        Assert.True(httpdata.Length > 2);
    }

    [SecretsFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetUserSponsorship()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var sponsorships = await client.QueryAsync(GraphQueries.ViewerSponsored);

        Assert.NotNull(sponsorships);

        foreach (var sponsorable in sponsorships)
        {
            var sponsorship = await client.QueryAsync(GraphQueries.ViewerSponsorship(sponsorable));

            Assert.NotNull(sponsorship);
            Assert.NotNull(sponsorship.Tier);
            Assert.NotEqual(0, sponsorship.Amount);
        }
    }

    [SecretsFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetUserSponsorships()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var sponsorships = await client.QueryAsync(GraphQueries.ViewerSponsorships);

        Assert.NotNull(sponsorships);
        Assert.NotEmpty(sponsorships);
    }

    [SecretsFact("SponsorLink:Account", "GitHub:Sponsorable")]
    public async Task GetUserOrOrganization()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Sponsorable");

        var tiers = await client.QueryAsync(GraphQueries.Tiers(Helpers.Configuration["SponsorLink:Account"]!));

        Assert.NotNull(tiers);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedContributions()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var user = await cli.QueryAsync(GraphQueries.ViewerAccount);
        Assert.NotNull(user);

        var httpdata = await http.QueryAsync(GraphQueries.UserContributions(user.Login, 5));
        var clidata = await cli.QueryAsync(GraphQueries.UserContributions(user.Login, 10));

        Assert.NotNull(httpdata);
        Assert.NotNull(clidata);

        var sortedhttp = new HashSet<string>(httpdata).OrderBy(x => x).ToArray();
        var sortedcli = new HashSet<string>(clidata).OrderBy(x => x).ToArray();

        Assert.Equal(sortedhttp, sortedcli);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedRepositoryContributions()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpcontribs = await http.QueryAsync(GraphQueries.CoreViewerContributedRepositories(5));
        var clicontribs = await cli.QueryAsync(GraphQueries.CoreViewerContributedRepositories(5));

        Assert.Equal(httpcontribs, clicontribs);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedOrganizationSponsorships()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpcontribs = await http.QueryAsync(GraphQueries.OrganizationSponsorships("github", 4));
        var clicontribs = await cli.QueryAsync(GraphQueries.OrganizationSponsorships("github", 4));

        Assert.True(httpcontribs?.Length > 4);
        Assert.Equal(httpcontribs, clicontribs);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedViewerOwnerContributions()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpcontribs = await http.QueryAsync(GraphQueries.CoreViewerContributedRepoOwners(2));
        var clicontribs = await cli.QueryAsync(GraphQueries.CoreViewerContributedRepoOwners(2));

        Assert.True(httpcontribs?.Length > 2);
        Assert.Equal(httpcontribs, clicontribs);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedUserSponsorships()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpdata = await http.QueryAsync(GraphQueries.UserSponsorships("sindresorhus", 2));
        var clidata = await cli.QueryAsync(GraphQueries.UserSponsorships("sindresorhus", 2));

        Assert.True(httpdata?.Length > 2);
        Assert.Equal(httpdata, clidata);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetPagedUserOrganizations()
    {
        EnsureAuthenticated();
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("davidfowl", 2));
        var httpdata = await cli.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("davidfowl", 2));

        Assert.True(httpdata?.Length > 2);
        Assert.Equal(httpdata, clidata);
    }

    [SecretsFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetAllSponsors()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var sponsors = await client.QueryAsync(GraphQueries.Sponsors(Helpers.Configuration["SponsorLink:Account"]!));

        Assert.NotNull(sponsors);
        Assert.NotEmpty(sponsors);
        Assert.All(sponsors, x => Assert.NotNull(x.Login));
        Assert.All(sponsors, x => Assert.NotNull(x.Tier));
    }

    void EnsureAuthenticated(string secret = "GitHub:Token")
    {
        Assert.True(TryExecute("gh", "auth login --with-token", Configuration[secret]!, out var output));
        Assert.True(TryExecute("gh", "auth token", out var token));
        Assert.Equal(Configuration[secret], token);
    }
}
