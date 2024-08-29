using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Devlooped.Sponsors;

partial class BackIssue(SponsorsManager sponsors, SponsoredIssues issues, IGitHubClient github, IConfiguration configuration, ILogger<BackIssue> logger)
{
    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
    static readonly ActivitySource tracer = ActivityTracer.Source;

    [Function("issues_list")]
    public async Task<HttpResponseData> ListIssues([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "github/issues")] HttpRequestData req)
    {
        using var activity = tracer.StartActivity("LinkIssues.Get");

        if (!configuration.TryGetClientId(logger, out var clientId))
            return req.CreateResponse(HttpStatusCode.InternalServerError);

        var response = req.CreateResponse(HttpStatusCode.OK);
        //response.Headers.Add("Content-Type", "text/html");
        // This allows subsequent requests to include auth cookies, such as the 'access_token' one set by the GitHubDevAuth one.
        // We need this always (not only for development) since the domain we get the token from is not the same as the docs site
        response.Headers.Add("Access-Control-Allow-Credentials", "true");

        var manifest = await sponsors.GetManifestAsync();

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal ||
            principal.FindFirstValue("urn:github:login") is not string login)
        {
            var issuer = manifest.Issuer;
            if (!issuer.StartsWith("https://"))
                issuer = $"https://{issuer}";
            if (!issuer.EndsWith("/"))
                issuer += "/";

            var referer = req.Headers.GetValues("Referer").FirstOrDefault() ?? "https://www.devlooped.com/SponsorLink/";
            var loginUrl = $"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&redirect_uri={issuer}.auth/login/github/callback&state=redir={referer.TrimEnd('/')}/github/issues.html?s={manifest.Sponsorable}&i={new Uri(issuer).Host}";

            await response.WriteAsJsonAsync(new { status = "unauthorized", loginUrl});
        }
        else
        {
            var sponsored = await issues.EnumerateSponsorships(login).ToListAsync();
#if DEBUG
            // add dummy sponsored funds/issues to sponsored list
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "1", 10, "devlooped/moq", 3689718, 1494));
            //sponsored.Add(new SponsoredIssues.IssueSponsor(login, "2", 20));
            //sponsored.Add(new SponsoredIssues.IssueSponsor(login, "3", 10));
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "4", 50, "devlooped/GitInfo", 36271064, 324));
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "5", 25, "devlooped/GitInfo", 36271064, 324));
            //sponsored.Add(new SponsoredIssues.IssueSponsor(login, "6", 100));
#endif

            var model = new Dictionary<string, long>();
            var repos = new Dictionary<long, Repository>();

            foreach (var item in sponsored.Where(x => x.Issue != null && x.RepositoryId != null))
            {
                if (!repos.TryGetValue(item.RepositoryId!.Value, out var repo))
                    repos[item.RepositoryId!.Value] = repo = await github.Repository.Get(item.RepositoryId!.Value);

                var key = $"{repo.Owner.Login}/{repo.Name}#{item.Issue}";
                if (model.TryGetValue(key, out var amount))
                    model[key] = amount + item.Amount;
                else
                    model[key] = item.Amount;
            }

            await response.WriteStringAsync(JsonSerializer.Serialize(new 
            { 
                status = "ok",
                sponsorable = manifest.Sponsorable,
                user = principal.FindFirstValue(ClaimTypes.GivenName) ?? principal.FindFirstValue(ClaimTypes.Name),
                available = sponsored.Where(x => x.Issue == null).Select(x => new { x.SponsorshipId, x.Amount }).ToArray(),
                backed = model.Select(x => new 
                { 
                    Issue = x.Key, 
                    Url = $"https://github.com/{x.Key[..x.Key.IndexOf('#')]}/issues/{x.Key[(x.Key.IndexOf('#')+1)..]}", 
                    Amount = x.Value 
                }).ToArray()
            }, options));
        }

        return response;
    }
}
