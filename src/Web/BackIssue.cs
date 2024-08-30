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

    record SponsorIssue(string Sponsorship, string Owner, string Repo, int Issue);

    // create a timer triggered function that runs every day at 4am and updates all backed issues
    [Function("issues_timer")]
    public async Task RefreshIssues([TimerTrigger("0 0 4 * * *")] TimerInfo timer) => await issues.RefreshBacked(github);

#if DEBUG
    [Function("issues_update")]
    public async Task RefreshIssuesForced([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "github/issues/update")] HttpRequestData req) 
        => await issues.RefreshBacked(github);
#endif

    [Function("issues_get")]
    public async Task<HttpResponseData> ListIssues([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "github/issues")] HttpRequestData req)
    {
        using var activity = tracer.StartActivity("LinkIssues.Get");

        var manifest = await sponsors.GetManifestAsync();

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal ||
            principal.FindFirstValue("urn:github:login") is not string login)
        {
            return await Unauthorized(req, manifest);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        var sponsored = await issues.EnumerateSponsorships(login).ToListAsync();

#if DEBUG
        // For testing purposes, we add some sponsorships if they don't exist.
        if (!sponsored.Any(x => x.SponsorshipId == "1"))
        {
            await issues.AddSponsorship(login, "1", 10);
            await issues.BackIssue(login, "1", "devlooped/moq", 3689718, 1494);
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "1", 10, "devlooped/moq", 3689718, 1494));
        }

        if (!sponsored.Any(x => x.SponsorshipId == "2"))
        {
            await issues.AddSponsorship(login, "2", 20);
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "2", 20));
        }

        if (!sponsored.Any(x => x.SponsorshipId == "3"))
        {
            await issues.AddSponsorship(login, "3", 10);
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "3", 10));
        }

        if (!sponsored.Any(x => x.SponsorshipId == "4"))
        {
            await issues.AddSponsorship(login, "4", 50);
            await issues.BackIssue(login, "4", "devlooped/GitInfo", 36271064, 324);
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "4", 50, "devlooped/GitInfo", 36271064, 324));
        }

        if (!sponsored.Any(x => x.SponsorshipId == "5"))
        {
            await issues.AddSponsorship(login, "5", 25);
            await issues.BackIssue(login, "5", "devlooped/GitInfo", 36271064, 324);
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "5", 25, "devlooped/GitInfo", 36271064, 324));
        }

        if (!sponsored.Any(x => x.SponsorshipId == "6"))
        {
            await issues.AddSponsorship(login, "6", 100);
            sponsored.Add(new SponsoredIssues.IssueSponsor(login, "6", 100));
        }
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
                Url = $"https://github.com/{x.Key[..x.Key.IndexOf('#')]}/issues/{x.Key[(x.Key.IndexOf('#') + 1)..]}",
                Amount = x.Value
            }).ToArray()
        }, options));

        return response;
    }

    [Function("issues_post")]
    public async Task<HttpResponseData> PostIssue([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "github/issues")] HttpRequestData req)
    {
        using var activity = tracer.StartActivity("LinkIssues.Set");

        var manifest = await sponsors.GetManifestAsync();

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal ||
            principal.FindFirstValue("urn:github:login") is not string login)
        {
            return await Unauthorized(req, manifest);
        }

        var json = await req.ReadAsStringAsync();

        if (string.IsNullOrEmpty(json))
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var issue = JsonSerializer.Deserialize<SponsorIssue>(json, options);
        if (issue == null)
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var repo = await github.Repository.Get(issue.Owner, issue.Repo);
        if (repo == null)
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var backed = await issues.BackIssue(login, issue.Sponsorship, repo.FullName, repo.Id, issue.Issue);
        if (!backed)
            return req.CreateResponse(HttpStatusCode.BadRequest);

        await issues.UpdateBacked(github, repo.Id, issue.Issue);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { status = "ok" });

        return response;
    }

    static async Task<HttpResponseData> Unauthorized(HttpRequestData request, SponsorableManifest manifest)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);

        var issuer = manifest.Issuer;
        if (!issuer.StartsWith("https://"))
            issuer = $"https://{issuer}";
        if (!issuer.EndsWith("/"))
            issuer += "/";

        var referer = request.Headers.GetValues("Referer").FirstOrDefault() ?? "https://www.devlooped.com/SponsorLink/";
        var loginUrl = $"https://github.com/login/oauth/authorize?client_id={manifest.ClientId}&scope=read:user%20read:org%20user:email&redirect_uri={issuer}.auth/login/github/callback&state=redir={referer.TrimEnd('/')}/github/issues.html?s={manifest.Sponsorable}&i={new Uri(issuer).Host}";

        await response.WriteAsJsonAsync(new { status = "unauthorized", loginUrl });
        return response;
    }
}
