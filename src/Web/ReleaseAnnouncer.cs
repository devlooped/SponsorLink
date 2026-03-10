using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

/// <summary>
/// Orchestrates the release announcement flow: dedup check → AI summarization →
/// thread formatting → X/Twitter posting → mark as announced.
/// </summary>
public class ReleaseAnnouncer(
    XClient xClient,
    ReleaseSummarizer summarizer,
    ReleaseAnnouncementFormatter formatter,
    ReleaseAnnouncementTracker tracker,
    ILogger<ReleaseAnnouncer> logger)
{
    public bool IsConfigured => summarizer.IsConfigured;

    public async Task AnnounceReleaseAsync(string owner, string repo, string tagName, string body, string releaseUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Release announcement not fully configured. Skipping.");
            return;
        }

        if (!xClient.IsConfigured)
        {
            logger.LogDebug("X client not configured. Skipping release announcement.");
            return;
        }

        if (await tracker.HasBeenAnnouncedAsync(owner, repo, tagName, cancellationToken))
        {
            logger.LogInformation("Release {Owner}/{Repo}@{Tag} already announced. Skipping.", owner, repo, tagName);
            return;
        }

        logger.LogInformation("Announcing release {Owner}/{Repo}@{Tag} to X", owner, repo, tagName);

        var plan = await summarizer.SummarizeReleaseAsync(tagName, body, releaseUrl, cancellationToken);
        if (plan == null)
        {
            logger.LogWarning("Failed to generate summary for {Owner}/{Repo}@{Tag}. Skipping announcement.", owner, repo, tagName);
            return;
        }

        var posts = formatter.FormatThread(plan, $"{owner}/{repo}", tagName, releaseUrl);
        if (posts.Count == 0)
        {
            logger.LogWarning("Formatter produced no posts for {Owner}/{Repo}@{Tag}.", owner, repo, tagName);
            return;
        }

        logger.LogInformation("Posting {Count} tweets for {Owner}/{Repo}@{Tag}", posts.Count, owner, repo, tagName);

        if (await xClient.PostThreadAsync(posts))
        {
            await tracker.MarkAnnouncedAsync(owner, repo, tagName, cancellationToken);
            logger.LogInformation("Successfully announced {Owner}/{Repo}@{Tag} to X", owner, repo, tagName);
        }
        else
        {
            logger.LogWarning("Failed to post thread for {Owner}/{Repo}@{Tag} to X", owner, repo, tagName);
        }
    }
}
