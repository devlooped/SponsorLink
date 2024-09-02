using System.Diagnostics;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using Octokit;

namespace Devlooped.Sponsors;

public static class SponsoredIssuesExtensions
{
    public static async Task UpdateBacked(this SponsoredIssues issues, IGitHubClient github, long? repository, int number)
    {
        if (repository is null)
            return;

        Issue issue;
        try
        {
            // Issue might have been deleted or not exist at all for some reason.
            issue = await github.Issue.Get(repository.Value, number);
        }
        catch (NotFoundException)
        {
            return;
        }
        catch (ApiException e) when (e.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            return;
        }

        var amount = await issues.BackedAmount(repository.Value, number);
        var updated = await issues.UpdateIssueBody(repository.Value, number, issue.Body);

        // Ensure the backed badge is present, regardless of the amount.
        if (updated != issue.Body)
        {
            await github.Issue.Update(repository.Value, number, new IssueUpdate
            {
                Body = updated,
            });
        }

        if (amount > 0 && !issue.Labels.Any(x => x.Name == "backed"))
        {
            // Apply backed label, with GH Sponsors official color.
            try
            {
                await github.Issue.Labels.Get(repository.Value, "backed");
            }
            catch (NotFoundException)
            {
                await github.Issue.Labels.Create(repository.Value, new NewLabel("backed", "EA4AAA")
                {
                    Description = "Backed via SponsorLink"
                });
            }

            await github.Issue.Labels.AddToIssue(repository.Value, number, ["backed"]);
        }
    }
}

public partial class SponsoredIssues
{
    readonly TableConnection table;
    readonly SponsorLinkOptions options;

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

        // Don't allow changing it once assigned.
        if (backed.Issue != null)
            return false;

        backed = backed with { Repository = repository, RepositoryId = repositoryId, Issue = issue };

        await repo.PutAsync(backed);

        // Indexed by repository|issue
        await TableRepository.Create<IssueSponsor>(table,
            x => x.RepositoryId + "|" + x.Issue!.ToString(),
            x => x.SponsorshipId)
            .PutAsync(backed);

        // Raw list of backed issues for periodic syning if needed to deal with potential 
        // concurrent funding/backing where we might overwritte an issue body badge.
        await TablePartition.Create(table, "backed")
            .PutAsync(new("backed", $"{repositoryId}|{issue}"));

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

    public async Task<string> UpdateIssueBody(long repository, int issue, string? body)
    {
        var amount = await BackedAmount(repository, issue);
        if (!string.IsNullOrEmpty(body))
        {
            var start = body.IndexOf("<!-- sl", StringComparison.Ordinal);
            if (start > 0)
            {
                var end = body.LastIndexOf("sl -->", StringComparison.Ordinal);
                if (end > 0)
                    body = body.Replace(body.Substring(start, end - start + "sl -->".Length), "");
                else
                    body = body.Substring(0, start);
            }
        }

        var domain = "https://www.devlooped.com";
#if DEBUG
        domain = "https://127.0.0.1:4000";
#endif
        // Brings in the optional baseurl from the _config.yml from docs folder.
        domain += ThisAssembly.Constants.DocsBaseUrl;

        var yaml =
            $"""


            <!-- sl -->
            [![Back this issue](https://raw.githubusercontent.com/devlooped/SponsorLink/main/docs/assets/img/separator.png "Back this issue")]({domain}/github/issues/?s={options.Account})
            [![Back this issue](https://img.shields.io/badge/backed-%24{amount}-EA4AAA?logo=githubsponsors "Back this issue")]({domain}/github/issues/?s={options.Account})
            <!-- sl -->
            """;

        return $"{body?.Trim()}{yaml}";
    }

    public async Task<int> RefreshBacked(IGitHubClient github)
    {
        var updated = new HashSet<string>();

        // Only consider recently updated issues.
        DateTimeOffset? startDate = DateTimeOffset.UtcNow.AddDays(-7);
        await foreach (var item in table.StorageAccount
            .CreateTableServiceClient()
            .GetTableClient(table.TableName)
            .QueryAsync<TableEntity>(x => x.PartitionKey == "backed" && x.Timestamp > startDate))
        {
            if (updated.Contains(item.RowKey))
                continue;

            var parts = item.RowKey.Split('|');
            var repository = long.Parse(parts[0]);
            var issue = int.Parse(parts[1]);

            await this.UpdateBacked(github, repository, issue);
            updated.Add(item.RowKey);
        }

        return updated.Count;
    }
}
