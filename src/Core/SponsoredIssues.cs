using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Devlooped.Sponsors;

public partial class SponsoredIssues
{
    readonly TableConnection table;
    SponsorLinkOptions options;

    public SponsoredIssues(CloudStorageAccount storage, IOptions<SponsorLinkOptions> options) : this(new TableConnection(storage, "BackedIssues"), options.Value) { }

    public SponsoredIssues(TableConnection connection, SponsorLinkOptions options) 
        => (table, this.options) = (connection, options);

    public record IssueSponsor([PartitionKey] string Account, [RowKey] string SponsorshipId, long Amount, string? Repository = null, long? RepositoryId = null, int? Issue = null);

    public Task AddSponsorship(string sponsor, string sponsorshipId, long amount)
        => TableRepository.Create<IssueSponsor>(table).PutAsync(new IssueSponsor(sponsor, sponsorshipId, amount));

    public IAsyncEnumerable<IssueSponsor> EnumerateSponsorships(string sponsor, CancellationToken cancellation = default)
        => TablePartition.Create<IssueSponsor>(table, sponsor).EnumerateAsync(cancellation);

    public async Task<bool> BackIssue(string account, string sponsorshipId, string repository, long repositoryId, int issue)
    {
        using var activity = ActivityTracer.Source.StartActivity();
        var repo = TableRepository.Create<IssueSponsor>(table);
        var backed = await repo.GetAsync(account, sponsorshipId);
        // If we can't find the funds with the given id, bail.
        if (backed == null || backed.Issue != null)
            return false;

        backed = backed with { Repository = repository, RepositoryId = repositoryId, Issue = issue };

        await repo.PutAsync(backed);

        // Indexed by repository|issue
        await TableRepository.Create<IssueSponsor>(table, 
            x => x.RepositoryId + "|" + x.Issue!.ToString(),
            x => x.SponsorshipId)
            .PutAsync(backed);

        activity?.AddEvent(new("Issue.Backed", tags: new ActivityTagsCollection(
            [
                KeyValuePair.Create<string, object?>("Amount", backed.Amount),
                KeyValuePair.Create<string, object?>("Repository", repository),
                KeyValuePair.Create<string, object?>("RepositoryId", repositoryId),
                KeyValuePair.Create<string, object?>("Issue", issue)
            ])));

        return true;
    }

    public async Task<long> BackedAmount(long repository, int issue)
    {
        var partition = TablePartition.Create<IssueSponsor>(table, 
            repository + "|" + issue, 
            x => x.SponsorshipId);

        var total = 0L;

        await foreach (var backed in partition.EnumerateAsync())
            total += backed.Amount;

        return total;
    }

    public async Task<string> UpdateIssueBody(long repository, int issue, string body)
    {
        var amount = await BackedAmount(repository, issue);
        var start = body.IndexOf("<!-- sl", StringComparison.Ordinal);
        if (start > 0)
        {
            var end = body.LastIndexOf("sl -->", StringComparison.Ordinal);
            if (end > 0)
                body = body.Replace(body.Substring(start, end - start + "sl -->".Length), "");
            else
                body = body.Substring(0, start);
        }

        var yaml =
            $"""


            <!-- sl -->
            [![Back this issue](https://raw.githubusercontent.com/devlooped/SponsorLink/main/docs/assets/img/separator.png "Back this issue")](https://www.devlooped.com/SponsorLink/github/issue.html?s={options.Account})
            [![Back this issue](https://img.shields.io/badge/backed-%24{amount}-EA4AAA?logo=githubsponsors "Back this issue")](https://www.devlooped.com/SponsorLink/github/issue.html?s={options.Account})
            <!-- sl -->
            """;

        return body.Trim() + yaml;
    }
}
