using System.Net;
using Humanizer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Devlooped.Sponsors;

public class Stats(AsyncLazy<OpenSource> oss, SponsorsManager manager)
{
    readonly TimeSpan expiration = TimeSpan.FromDays(1);

    [Function("nuget-count")]
    public async Task<HttpResponseData> NuGetCountAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nuget/all")] HttpRequestData req)
    {
        var stats = await oss;
        var manifest = await manager.GetManifestAsync();
        var count = 0;

        if (req.Query.Count == 1)
        {
            // Sum all packages across all repositories contributed to by the author in the querystring
            if (req.Query.ToString() is { } author && 
                stats.Authors.TryGetValue(author, out var repositories))
            {
                count = stats.Packages
                    .Where(x => repositories.Contains(x.Key))
                    .SelectMany(x => x.Value.Keys)
                    .Count();
            }
        }
        else
        {
            count = stats.Packages
                .Where(x => x.Key.StartsWith(manifest.Sponsorable + "/"))
                .Sum(x => x.Value.Count);
        }

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
        var count = 0l;

        if (req.Query.Count == 1)
        {
            // Sum all packages across all repositories contributed to by the author in the querystring
            if (req.Query.ToString() is { } author &&
                stats.Authors.TryGetValue(author, out var repositories))
            {
                count = stats.Packages
                    .Where(x => repositories.Contains(x.Key))
                    .Sum(x => x.Value.Sum(x => x.Value));
            }
        }
        else
        {
            count = stats.Packages
                .Where(x => x.Key.StartsWith(manifest.Sponsorable + "/"))
                .Sum(x => x.Value.Sum(y => y.Value));
        }

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