using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SharpYaml;

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
public partial class ReleaseAnnouncer(
    XClient xClient,
    ReleaseSummarizer summarizer,
    ReleaseAnnouncementFormatter formatter,
    ILogger<ReleaseAnnouncer> logger)
{
    static readonly List<string> releaseTitles;

    static ReleaseAnnouncer()
    {
        var dict = YamlSerializer.Deserialize<Dictionary<string, object>>(ThisAssembly.Resources.release.Text);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(dict));
        releaseTitles = [.. Devlooped.Jq.Evaluate(".. | .title? // empty", json.RootElement).Select(x => x.ToString())];
    }

    public bool IsConfigured => summarizer.IsConfigured;

    public Task<bool> AnnounceReleaseAsync(AnnounceRelease release, CancellationToken cancellationToken = default)
        => AnnounceReleaseAsync(release.Owner, release.Repo, release.TagName, release.Body, release.ReleaseUrl, cancellationToken);


    /// <summary>The <paramref name="body"/> contains an HTML comment with a single X in it, to force publish even edited releases.</summary>
    public static bool HasForceAnnounce(string? body) => !string.IsNullOrEmpty(body) && ForceAnnounceExpr().IsMatch(body);

    /// <summary>The <paramref name="body"/> contains an HTML comment with a single X in it, to force publish even edited releases.</summary>
    public static bool HasSkipAnnounce(string? body) => !string.IsNullOrEmpty(body) && NoAnnounceExpr().IsMatch(body);

    [GeneratedRegex(@"\<!--\s+[xX]\s+--\>")]
    private static partial Regex ForceAnnounceExpr();

    [GeneratedRegex(@"\<!--\s+![xX]\s+--\>")]
    private static partial Regex NoAnnounceExpr();

    public async Task<bool> AnnounceReleaseAsync(string owner, string repo, string tagName, string body, string releaseUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("Release announcement not fully configured. Skipping.");
            return false;
        }

        if (!xClient.IsConfigured)
        {
            logger.LogWarning("X client not configured. Skipping release announcement.");
            return false;
        }

        if (NoAnnounceExpr().IsMatch(body))
        {
            logger.LogWarning("Release body contains no-announce marker. Skipping announcement for {Owner}/{Repo}@{Tag}.", owner, repo, tagName);
            return false;
        }

        // NOTE: if we have the force announce comment, we don't check for anything else in the body.
        if (!HasForceAnnounce(body) && !releaseTitles.Any(title => body.Contains(title, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("Release body does not match any release section titles for publication. Skipping announcement for {Owner}/{Repo}@{Tag}.", tagName, owner, repo);
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
