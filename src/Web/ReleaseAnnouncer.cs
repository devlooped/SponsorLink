using System.Security.Principal;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

public record AnnounceRelease(string Owner, string Repo, string TagName, string Body, string ReleaseUrl);

public class ReleaseAnnouncerFunctions(ReleaseAnnouncer announcer, CloudStorageAccount storage, ILogger<ReleaseAnnouncer> logger)
{
    public const string QueueName = "announcer";
    readonly ITableRepository<TableEntity> table = TableRepository.Create(storage, QueueName);

    [Function("announcer_dequeue")]
    public async Task DequeueAsync([QueueTrigger(QueueName, Connection = "AzureWebJobsStorage")] string json)
    {
        if (JsonSerializer.Deserialize<AnnounceRelease>(json) is AnnounceRelease release)
        {
            if (await table.GetAsync($"{release.Owner}_{release.Repo}", release.TagName) is not null)
            {
                logger.LogInformation("Release {Owner}/{Repo}@{Tag} already announced. Skipping.", release.Owner, release.Repo, release.TagName);
                return;
            }

            if (await announcer.AnnounceReleaseAsync(release))
                await table.PutAsync(new TableEntity($"{release.Owner}_{release.Repo}", release.TagName));
        }
        else
            logger.LogWarning("Failed to deserialize release announcement from queue: {Json}", json);
    }
}

/// <summary>
/// Orchestrates the release announcement flow: dedup check → AI summarization →
/// thread formatting → X/Twitter posting → mark as announced.
/// </summary>
public class ReleaseAnnouncer(
    XClient xClient,
    ReleaseSummarizer summarizer,
    ReleaseAnnouncementFormatter formatter,
    ILogger<ReleaseAnnouncer> logger)
{
    public bool IsConfigured => summarizer.IsConfigured;

    public Task<bool> AnnounceReleaseAsync(AnnounceRelease release, CancellationToken cancellationToken = default)
        => AnnounceReleaseAsync(release.Owner, release.Repo, release.TagName, release.Body, release.ReleaseUrl, cancellationToken);

    public async Task<bool> AnnounceReleaseAsync(string owner, string repo, string tagName, string body, string releaseUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Release announcement not fully configured. Skipping.");
            return false;
        }

        if (!xClient.IsConfigured)
        {
            logger.LogDebug("X client not configured. Skipping release announcement.");
            return false;
        }

        logger.LogInformation("Announcing release {Owner}/{Repo}@{Tag} to X", owner, repo, tagName);

        var plan = await summarizer.SummarizeReleaseAsync(tagName, body, releaseUrl, cancellationToken);
        if (plan == null)
        {
            logger.LogWarning("Failed to generate summary for {Owner}/{Repo}@{Tag}. Skipping announcement.", owner, repo, tagName);
            return false;
        }

        var posts = formatter.FormatThread(plan, $"{owner}/{repo}", tagName, releaseUrl);
        if (posts.Count == 0)
        {
            logger.LogWarning("Formatter produced no posts for {Owner}/{Repo}@{Tag}.", owner, repo, tagName);
            return false;
        }

        logger.LogInformation("Posting {Count} tweets for {Owner}/{Repo}@{Tag}", posts.Count, owner, repo, tagName);

        if (await xClient.PostThreadAsync(posts))
        {
            logger.LogInformation("Successfully announced {Owner}/{Repo}@{Tag} to X", owner, repo, tagName);
            return true;
        }
        else
        {
            logger.LogWarning("Failed to post thread for {Owner}/{Repo}@{Tag} to X", owner, repo, tagName);
            return false;
        }
    }
}
