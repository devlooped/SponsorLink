using Devlooped.Sponsors;
using Microsoft.Extensions.DependencyInjection;
using static Devlooped.Helpers;

namespace Devlooped.Tests;

public class GraphQueriesTests
{
    [SecretsFact("GitHub:Token")]
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
        Assert.All(orgs, org => candidates.Contains(org.Login));
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

        var orgs = await client.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("paxan"));

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
        Assert.True(sponsorships.Count() > 2);
    }

    [LocalFact("SponsorLink:Account")]
    public async Task GetCliSponsorships()
    {
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
        Assert.True(sponsorships.Count() > 2);
    }

    [LocalFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetPagedSponsorships()
    {
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
        Assert.True(httpdata.Count() > 2);
    }

    async Task<Organization[]?> GetSponsoringOrganizations(IGraphQueryClient client, Account account, string sponsorable, int pageSize = 100)
    {
        var sponsorships = account.Type == Sponsors.AccountType.User ?
            await client.QueryAsync(GraphQueries.SponsoringOrganizationsForUser(sponsorable, pageSize)) :
            await client.QueryAsync(GraphQueries.SponsoringOrganizationsForOrg(sponsorable, pageSize));

        Assert.NotNull(sponsorships);
        Assert.NotEmpty(sponsorships);

        return sponsorships;
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedViewerSponsored()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync(GraphQueries.CoreViewerSponsored(2));
        var httpdata = await http.QueryAsync(GraphQueries.CoreViewerSponsored(2));

        Assert.NotNull(clidata);
        Assert.NotNull(httpdata);
        Assert.Equal(clidata, httpdata);

        // NOTE: this would only pass if you have at least 2 sponsorships
        Assert.True(httpdata.Count() > 2);
    }

    [LocalFact("GitHub:Token")]
    public async Task GetPagedViewerSponsorships()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync(GraphQueries.CoreViewerSponsorships(2));
        var httpdata = await http.QueryAsync(GraphQueries.CoreViewerSponsorships(2));

        Assert.NotNull(clidata);
        Assert.NotNull(httpdata);
        Assert.Equal(clidata, httpdata);

        // NOTE: this would only pass if you have at least 2 sponsorships
        Assert.True(httpdata.Count() > 2);
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

    [LocalFact]
    public async Task GetPagedContributions()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpdata = await http.QueryAsync(GraphQueries.UserContributions("kzu", 5));
        var clidata = await cli.QueryAsync(GraphQueries.UserContributions("kzu", 10));

        Assert.NotNull(httpdata);
        Assert.NotNull(clidata);

        var sortedhttp = new HashSet<string>(httpdata).OrderBy(x => x).ToArray();
        var sortedcli = new HashSet<string>(clidata).OrderBy(x => x).ToArray();

        Assert.Equal(sortedhttp, sortedcli);
    }

    [LocalFact]
    public async Task GetPagedRepositoryContributions()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpcontribs = await http.QueryAsync(GraphQueries.CoreViewerContributedRepositories(5));
        var clicontribs = await cli.QueryAsync(GraphQueries.CoreViewerContributedRepositories(5));

        Assert.Equal(httpcontribs, clicontribs);
    }

    [LocalFact]
    public async Task GetPagedOrganizationSponsorships()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpcontribs = await http.QueryAsync(GraphQueries.OrganizationSponsorships("github", 4));
        var clicontribs = await cli.QueryAsync(GraphQueries.OrganizationSponsorships("github", 4));

        Assert.True(httpcontribs?.Count() > 4);
        Assert.Equal(httpcontribs, clicontribs);
    }

    [LocalFact]
    public async Task GetPagedViewerOwnerContributions()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpcontribs = await http.QueryAsync(GraphQueries.CoreViewerContributedRepoOwners(2));
        var clicontribs = await cli.QueryAsync(GraphQueries.CoreViewerContributedRepoOwners(2));

        Assert.True(httpcontribs?.Count() > 2);
        Assert.Equal(httpcontribs, clicontribs);
    }

    [LocalFact]
    public async Task GetPagedUserSponsorships()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var httpdata = await http.QueryAsync(GraphQueries.UserSponsorships("sindresorhus", 2));
        var clidata = await cli.QueryAsync(GraphQueries.UserSponsorships("sindresorhus", 2));

        Assert.True(httpdata?.Count() > 2);
        Assert.Equal(httpdata, clidata);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetPagedUserOrganizations()
    {
        var cli = new CliGraphQueryClient();
        var http = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub:Token");

        var clidata = await cli.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("davidfowl", 2));
        var httpdata = await cli.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("davidfowl", 2));

        Assert.True(httpdata?.Count() > 2);
        Assert.Equal(httpdata, clidata);
    }
}
