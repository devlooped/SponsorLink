using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Azure.Identity;
using Azure.Monitor.Query;
using Microsoft.Extensions.Logging;

namespace Devlooped.SponsorLink;

public class Stats
{
    readonly CloudStorageAccount storageAccount;
    readonly ILogger logger;


    public Stats(CloudStorageAccount storageAccount, ILogger logger) 
        => (this.storageAccount, this.logger)
        = (storageAccount, logger);    

    [FunctionName("users")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "stats/{query}")] HttpRequestMessage req, string query)
    {
        var blobs = storageAccount.CreateBlobServiceClient().GetBlobContainerClient("sponsorlink");
        if (await blobs.ExistsAsync() == false)
            return new NotFoundResult();

        var blob = blobs.GetBlobClient($"queries/{query}.kql");
        if (await blob.ExistsAsync() == false)
        {
            logger.LogWarning("Got stats query {query} which doesn't exist in storage.", query);
            return new OkObjectResult(new
            {
                schemaVersion = 1,
                isError = true,
                label = "error",
                message = "404",
            });
        }

        var kql = await blob.DownloadContentAsync();
        var creds = new DefaultAzureCredential();
        var client = new LogsQueryClient(creds);

        var result = await client.QueryWorkspaceAsync<long>(
            Constants.LogAnalyticsWorkspaceId, kql.Value.Content.ToString(),
            QueryTimeRange.All);

        if (result.Value.Count == 0)
        {
            logger.LogWarning("Query {query}.kql resulted in an empty response.", query);
            return new OkObjectResult(new
            {
                schemaVersion = 1,
                isError = true,
                label = "empty",
                message = "204",
            });
        }

        return new OkObjectResult(new
        {
            schemaVersion = 1,
            label = "",
            message = result.Value[0].ToString(),
        });
    }
}
