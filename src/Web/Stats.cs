using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using System.Net;
using Humanizer;

namespace Devlooped.Sponsors;

public class Stats(AsyncLazy<OpenSource> oss, SponsorsManager manager)
{
    readonly TimeSpan expiration = TimeSpan.FromDays(1);

    [Function("nuget-count")]
    public async Task<HttpResponseData> NuGetCountAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nuget/all")] HttpRequestData req)
    {
        var stats = await oss;
        var manifest = await manager.GetManifestAsync();
        var owner = req.Query.Count == 1 ? req.Query.ToString() : manifest.Sponsorable;

        var count = stats.Packages
            .Where(x => x.Key.StartsWith(owner + "/"))
            .Sum(x => x.Value.Count);

        var output = req.CreateResponse(HttpStatusCode.OK);

        // Also cache downstream (specifically shields.io)
        output.Headers.Add("Cache-Control", "public,max-age=" + expiration.TotalSeconds);
        await output.WriteAsJsonAsync(new
        {
            schemaVersion = 1,
            label = "nugets",
            message = ((double)count).ToMetric(decimals: 1)
        });

        return output;
    }

    [Function("nuget-downloads")]
    public async Task<HttpResponseData> NuGetDownloadsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nuget/dl")] HttpRequestData req)
    {
        var stats = await oss;
        var manifest = await manager.GetManifestAsync();
        var owner = req.Query.Count == 1 ? req.Query.ToString() : manifest.Sponsorable;

        var count = stats.Packages
            .Where(x => x.Key.StartsWith(owner + "/"))
            .Sum(x => x.Value.Sum(y => y.Value));

        var output = req.CreateResponse(HttpStatusCode.OK);

        // Also cache downstream (specifically shields.io)
        output.Headers.Add("Cache-Control", "public,max-age=" + expiration.TotalSeconds);
        await output.WriteAsJsonAsync(new
        {
            schemaVersion = 1,
            label = "dl/day",
            message = ((double)count).ToMetric(decimals: 1)
        });

        return output;
    }

}