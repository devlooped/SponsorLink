using System.Net;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Devlooped.Sponsors;

public class Badge(IHttpClientFactory httpFactory, IMemoryCache cache, IOptions<SponsorLinkOptions> options, ILogger<Badge> logger)
{
    SponsorLinkOptions options = options.Value;
    TimeSpan expiration = TimeSpan.TryParse(options.Value.BadgeExpiration, out var expiration) ? expiration : TimeSpan.FromMinutes(5);

    // Optional endpoint that uses SponsorLink:LogAnalytics workspace ID to query for usage stats
    // In a format compatible with shields.io badges.
    [Function("badge")]
    public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var workspace = options.LogAnalytics;
        if (string.IsNullOrEmpty(workspace))
        {
            logger.LogError("Missing SponsorLink:LogsAnalytics configuration");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        var type = req.Query.Count == 1 ? req.Query.ToString() : nameof(SponsorTypes.User);
        // Account for abbreviations
        type = type switch
        {
            "org" => nameof(SponsorTypes.Organization),
            "contrib" => nameof(SponsorTypes.Contributor),
            _ => type
        };

        if (!Enum.TryParse<SponsorTypes>(type, true, out var typed))
        {
            logger.LogError("Invalid sponsor type: {type}", type);
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var cacheKey = "Badge." + typed;

        if (!cache.TryGetValue<JObject>(cacheKey, out var stats) || stats is null)
        {
            var token = await new DefaultAzureCredential().GetTokenAsync(new TokenRequestContext(["https://api.loganalytics.io/.default"]));
            using var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            var response = await http.GetAsync($"https://api.loganalytics.io/v1/workspaces/{workspace}/query?query=" +
                $"""
                AppEvents
                | union AppRequests
                | where isnotempty(SessionId) and SessionId != 'devlooped.sponsors.ci'
                | where Name == "Sponsor.Lookup"
                | extend Sponsor = tostring(Properties['Type'])
                | summarize arg_max(Sponsor, *) by SessionId
                | extend Types = parse_csv(Sponsor)
                | mv-expand Types
                | where Types contains '{typed}'
                | summarize Count = count() by tostring(Types)
                | summarize Total = sum(Count)
                """);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to query for badge stats: {status}", response.StatusCode);
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }

            var json = await response.Content.ReadAsStringAsync();
            stats = JObject.Parse(json);
            cache.Set(cacheKey, stats, expiration);
        }

        var output = req.CreateResponse(HttpStatusCode.OK);
        // Also cache downstream (specifically shields.io)
        output.Headers.Add("Cache-Control", "public,max-age=" + expiration.TotalSeconds);
        await output.WriteAsJsonAsync(new
        {
            schemaVersion = 1,
            label = typed.ToString(),
            message = stats.SelectToken("$.tables[0].rows[0][0]")?.ToString() ?? "0",
        });

        return output;
    }
}
