using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Devlooped.Sponsors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static Devlooped.Helpers;

namespace Devlooped.Tests;

public class GraphQueriesTests
{
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
        Assert.All(orgs, org => candidates.Contains(org.Login));
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetUserOrganizations()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var orgs = await client.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("davidfowl"));

        Assert.NotNull(orgs);
        Assert.Contains(orgs, x => x.Login == "dotnet");
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetUserOrganizations2()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var orgs = await client.QueryAsync<Organization[]>(GraphQueries.UserOrganizations("paxan"));

        Assert.NotNull(orgs);
        Assert.Contains(orgs, x => x.Login == "aws");
    }

    [SecretsFact("SponsorLink:Account")]
    public async Task GetSponsorships()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

        var sponsorships = await client.QueryAsync(GraphQueries.VerifiedSponsoringOrganizations(Helpers.Configuration["SponsorLink:Account"]!));

        Assert.NotNull(sponsorships);
        Assert.NotEmpty(sponsorships);
    }

    [SecretsFact("SponsorLink:Account", "GitHub:Token")]
    public async Task GetUserSponsorship()
    {
        var client = new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub");

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
}
