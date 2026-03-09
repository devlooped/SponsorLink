using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

/// <summary>
/// Tracks which releases have been announced to X/Twitter using Azure Blob Storage,
/// preventing duplicate announcements on re-delivered webhooks.
/// </summary>
public class ReleaseAnnouncementTracker
{
    const string ContainerName = "release-announcements";

    readonly BlobContainerClient container;
    readonly ILogger<ReleaseAnnouncementTracker> logger;

    public ReleaseAnnouncementTracker(IConfiguration configuration, ILogger<ReleaseAnnouncementTracker> logger)
    {
        this.logger = logger;
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("Missing required configuration 'AzureWebJobsStorage'.");
        var blobService = new BlobServiceClient(connectionString);
        container = blobService.GetBlobContainerClient(ContainerName);
    }

    public async Task<bool> HasBeenAnnouncedAsync(string owner, string repo, string tagName, CancellationToken cancellationToken = default)
    {
        try
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var blobName = $"{owner}/{repo}/{tagName}".ToLowerInvariant();
            var blob = container.GetBlobClient(blobName);
            return await blob.ExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error checking announcement state for {Owner}/{Repo}@{Tag}", owner, repo, tagName);
            return false;
        }
    }

    public async Task MarkAnnouncedAsync(string owner, string repo, string tagName, CancellationToken cancellationToken = default)
    {
        try
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            var blobName = $"{owner}/{repo}/{tagName}".ToLowerInvariant();
            var blob = container.GetBlobClient(blobName);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")));
            await blob.UploadAsync(stream, overwrite: true, cancellationToken);
            logger.LogInformation("Marked release as announced: {Owner}/{Repo}@{Tag}", owner, repo, tagName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving announcement state for {Owner}/{Repo}@{Tag}", owner, repo, tagName);
        }
    }
}
