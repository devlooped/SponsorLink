using System.Net;
using Humanizer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Devlooped.Sponsors;

public class Stats(AsyncLazy<OpenSource> oss, IGraphQueryClientFactory graph, SponsorsManager manager)
{
    readonly TimeSpan expiration = TimeSpan.FromDays(1);

    [Function("nuget-count")]
    public async Task<HttpResponseData> NuGetCountAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nuget/id")] HttpRequestData req)
    {
        var stats = await oss;
        var manifest = await manager.GetManifestAsync();
        var count = 0;
        var by = manifest.Sponsorable;

        if (req.Query.Count == 0)
        {
            var owner = manifest.Sponsorable + "/";
            count = stats.Packages
                .Where(x => x.Key.StartsWith(owner))
                .Sum(x => x.Value.Count);
        }
        else
        {
            // Single-account shorthand used by the static page, where we need to disambiguate user vs org accounts
            if (req.Query.Get("a") is string a)
            {
                if (await graph.CreateClient("sponsorable").QueryAsync(GraphQueries.FindAccount(a)) is { } account)
                {
                    if (account.Type == AccountType.Organization)
                        req.Query["owner"] = account.Login;
                    else
                        req.Query["author"] = account.Login;
                }
                else
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
            }

            // now we can either have ?a={author} or ?o={owner}
            if (req.Query.GetValues("author") is { Length: > 0 } authors)
            {
                // Sum all (unique) packages across all repositories contributed to by
                // the authors in the querystring
                count = stats.Authors.Where(x => authors.Contains(x.Key))
                    // First all repos by all authors
                    .SelectMany(x => x.Value)
                    // Deduplicate the repos
                    .Distinct()
                    // Then all packages in those repos
                    .SelectMany(x => stats.Packages[x])
                    // Deduplicate the packages
                    .Distinct()
                    .Count();

                by = string.Join(",", authors);
            }
            else if (req.Query.GetValues("owner") is { Length: > 0 } values)
            {
                var owners = values.Select(x => x += "/").ToHashSet();
                by = string.Join(",", values);
                count = stats.Packages
                    // filter those that start with each of the owners
                    .Where(x => owners.Any(o => x.Key.StartsWith(o)))
                    .Sum(x => x.Value.Count);
            }
            else
            {
                // Default to sponsorable packages for cases where we didn't get neither authors nor owners
                var owner = manifest.Sponsorable + "/";
                count = stats.Packages
                    .Where(x => x.Key.StartsWith(owner))
                    .Sum(x => x.Value.Count);
            }
        }

        var output = req.CreateResponse(HttpStatusCode.OK);

        // Also cache downstream (specifically shields.io)
        output.Headers.Add("Cache-Control", "public,max-age=" + expiration.TotalSeconds);
        await output.WriteAsJsonAsync(new
        {
            schemaVersion = 1,
            label = $"{by} nugets",
            message = ((double)count).ToMetric(decimals: 1)
        });

        return output;
    }

    [Function("nuget-downloads")]
    public async Task<HttpResponseData> NuGetDownloadsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "nuget/dl")] HttpRequestData req)
    {
        var stats = await oss;
        var manifest = await manager.GetManifestAsync();
        var count = 0L;
        var by = manifest.Sponsorable;

        if (req.Query.Count == 0)
        {
            var owner = manifest.Sponsorable + "/";
            count = stats.Packages
                .Where(x => x.Key.StartsWith(owner))
                .Sum(x => x.Value.Sum(s => s.Value));
        }
        else
        {
            // Single-account shorthand used by the static page, where we need to disambiguate user vs org accounts
            if (req.Query.Get("a") is string a)
            {
                if (await graph.CreateClient("sponsorable").QueryAsync(GraphQueries.FindAccount(a)) is { } account)
                {
                    if (account.Type == AccountType.Organization)
                        req.Query["owner"] = account.Login;
                    else
                        req.Query["author"] = account.Login;
                }
                else
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
            }

            // now we can either have ?a={author} or ?o={owner}
            if (req.Query.GetValues("author") is { Length: > 0 } authors)
            {
                // Sum all (unique) packages across all repositories contributed to by
                // the authors in the querystring
                count = stats.Authors.Where(x => authors.Contains(x.Key))
                    // First all repos by all authors
                    .SelectMany(x => x.Value)
                    // Deduplicate the repos
                    .Distinct()
                    // Then all packages in those repos
                    .SelectMany(x => stats.Packages[x])
                    // Deduplicate the packages
                    .Distinct()
                    .Sum(x => x.Value);

                by = string.Join(",", authors);
            }
            else if (req.Query.GetValues("owner") is { Length: > 0 } values)
            {
                var owners = values.Select(x => x += "/").ToHashSet();
                by = string.Join(",", values);
                count = stats.Packages
                    // filter those that start with each of the owners
                    .Where(x => owners.Any(o => x.Key.StartsWith(o)))
                    .Sum(x => x.Value.Sum(s => s.Value));
            }
            else
            {
                // Default to sponsorable packages for cases where we didn't get neither authors nor owners
                var owner = manifest.Sponsorable + "/";
                count = stats.Packages
                    .Where(x => x.Key.StartsWith(owner))
                    .Sum(x => x.Value.Sum(s => s.Value));
            }
        }

        var output = req.CreateResponse(HttpStatusCode.OK);

        // Also cache downstream (specifically shields.io)
        output.Headers.Add("Cache-Control", "public,max-age=" + expiration.TotalSeconds);
        await output.WriteAsJsonAsync(new
        {
            schemaVersion = 1,
            label = $"{by} dl/day",
            message = ((double)count).ToMetric(decimals: 1)
        });

        return output;
    }

}