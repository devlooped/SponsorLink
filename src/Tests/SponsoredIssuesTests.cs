using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Devlooped.Sponsors;
using Microsoft.Extensions.DependencyInjection;
using static Devlooped.Helpers;

namespace Devlooped.Tests;

public class SponsoredIssuesTests : IDisposable
{
    TableConnection? table;

    public void Dispose()
    {
        if (table != null)
        {
            var result = CloudStorageAccount.DevelopmentStorageAccount
                .CreateTableServiceClient()
                .DeleteTable(table.TableName);

            // ensure result.Status is a success status code
            Assert.True(result.Status >= 200 && result.Status < 300);
        }

        table = null;
    }

    [Fact]
    public async Task NewSponsorship()
    {
        var sponsored = new SponsoredIssues(GetTable(), Services.GetRequiredService<SponsorLinkOptions>());
        await sponsored.AddSponsorship("kzu", "S_kwHOA6rues4AAwoC", 10);
        await sponsored.BackIssue("kzu", "S_kwHOA6rues4AAwoC", "owner/repo", 42, 1234);

        var amount = await sponsored.BackedAmount(42, 1234);
        Assert.Equal(10, amount);
    }

    [Fact]
    public async Task AggregatesSponsorships()
    {
        var sponsored = new SponsoredIssues(GetTable(), Services.GetRequiredService<SponsorLinkOptions>());
        await sponsored.AddSponsorship("kzu", "S_kwHOA6rues4AAwoC", 10);
        await sponsored.BackIssue("kzu", "S_kwHOA6rues4AAwoC", "owner/repo", 42, 1234);

        await sponsored.AddSponsorship("user", "S_asdf", 20);
        await sponsored.BackIssue("user", "S_asdf", "owner/repo", 42, 1234);

        var amount = await sponsored.BackedAmount(42, 1234);
        Assert.Equal(30, amount);
    }

    [Fact]
    public async Task EnumerateSponsorships()
    {
        var sponsored = new SponsoredIssues(GetTable(), Services.GetRequiredService<SponsorLinkOptions>());
        await sponsored.AddSponsorship("kzu", "S_kwHOA6rues4AAwoC", 10);
        await sponsored.AddSponsorship("kzu", "S_kwHOA6rues4AAwoD", 20);
        await sponsored.AddSponsorship("kzu", "S_kwHOA6rues4AAwoE", 15);

        var issues = await sponsored.EnumerateSponsorships("kzu", CancellationToken.None).ToListAsync();

        var total = issues.Aggregate(0L, (acc, i) => acc + i.Amount);
        Assert.Equal(45, total);
    }

    [Fact]
    public async Task AddsBackedAmountBadge()
    {
        var sponsored = new SponsoredIssues(GetTable(), Services.GetRequiredService<SponsorLinkOptions>());
        await sponsored.AddSponsorship("kzu", "1", 10);
        await sponsored.BackIssue("kzu", "1","owner/repo", 42, 12345);

        var body = await sponsored.UpdateIssueBody(42, 12345, "Body");

        Assert.Single(Regex.Matches(body, "https://img.shields.io/badge/backed-"));
        Assert.Contains("backed-%2410", body);

        await sponsored.AddSponsorship("kzu", "2", 20);
        await sponsored.BackIssue("kzu", "2", "owner/repo", 42, 12345);

        body = await sponsored.UpdateIssueBody(42, 12345, body);

        Assert.Single(Regex.Matches(body, "https://img.shields.io/badge/backed-"));
        Assert.Contains("backed-%2430", body);
    }

    TableConnection GetTable([CallerMemberName] string? test = default)
        => table = new TableConnection(CloudStorageAccount.DevelopmentStorageAccount, $"{nameof(SponsoredIssuesTests)}{test}");
}
