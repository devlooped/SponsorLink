using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Storage.Queues;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.IssueComment;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Events.Release;
using Octokit.Webhooks.Events.Sponsorship;
using Octokit.Webhooks.Models;

namespace Devlooped.Sponsors;

public partial class Webhook(SponsorsManager manager, SponsoredIssues issues, IConfiguration config, IGitHubClient github, IPushover notifier, ILogger<Webhook> logger, IHttpClientFactory httpFactory, QueueServiceClient queues, IServiceProvider services) : WebhookEventProcessor
{
    static readonly ActivitySource tracer = ActivityTracer.Source;

    protected override async ValueTask ProcessSponsorshipWebhookAsync(WebhookHeaders headers, SponsorshipEvent payload, SponsorshipAction action, CancellationToken cancellationToken = default)
    {
        using var activity = tracer.StartActivity("Sponsorship");
        activity?.AddEvent(new ActivityEvent($"{activity?.OperationName}.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action)}"));
        manager.RefreshSponsors();

        if (action == SponsorshipAction.Created && payload.Sponsorship.Tier.IsOneTime)
        {
            await issues.AddSponsorship(
                payload.Sponsorship.Sponsor.Login,
                payload.Sponsorship.NodeId,
                payload.Sponsorship.Tier.MonthlyPriceInDollars);
        }

        await base.ProcessSponsorshipWebhookAsync(headers, payload, action);
    }

    protected override async ValueTask ProcessReleaseWebhookAsync(WebhookHeaders headers, ReleaseEvent payload, ReleaseAction action, CancellationToken cancellationToken = default)
    {
        await base.ProcessReleaseWebhookAsync(headers, payload, action);

        // Don't re-process something we did ourselves by checking if the sender is the same as the authenticated
        // user used to make changes to the release.
        if (await github.User.Current() is { } user && payload.Sender?.Login == user.Login)
            return;

        if (action != ReleaseAction.Deleted)
        {
            // fetch sponsors markdown from https://github.com/devlooped/sponsors/raw/refs/heads/main/sponsors.md
            // lookup for <!-- sponsors --> and <!-- /sponsors --> markers
            // replace that section in the release body with the markdown

            try
            {
                using var activity = tracer.StartActivity("Release");
                activity?.AddEvent(new ActivityEvent($"{activity?.OperationName}.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action)}"));

                var body = payload.Release.Body ?? string.Empty;
                if (body.Contains("<!-- nosponsors -->"))
                    return;

                const string startMarker = "<!-- sponsors -->";
                const string endMarker = "<!-- /sponsors -->";

                // Get sponsors markdown
                using var http = new HttpClient();
                var sponsorsMarkdown = await http.GetStringAsync("https://github.com/devlooped/sponsors/raw/refs/heads/main/sponsors.md", cancellationToken);
                if (string.IsNullOrWhiteSpace(sponsorsMarkdown))
                    return;

                var logins = LoginExpr().Matches(sponsorsMarkdown)
                    .Select(x => x.Groups["login"].Value)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => "@" + x)
                    .Distinct();

                var newSection =
                    $"""
                    <!-- avoid this section by leaving a nosponsors tag -->
                    ## Sponsors

                    The following sponsors made this release possible: {string.Join(", ", logins)}.

                    Thanks 💜
                    """;

                // NOTE: no need to append the images since GH already does this by showing them in a 
                // Contributors generated section.
                // {string.Concat(sponsorsMarkdown.ReplaceLineEndings().Replace(Environment.NewLine, ""))}

                // In case we want to split into rows of X max icons instead...
                //+ string.Join(
                //    Environment.NewLine,
                //    sponsorsMarkdown.ReplaceLineEndings()
                //        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                //        .Batch(15)
                //        .Select(batch => string.Concat(batch.Select(s => s.Trim())).Trim()));

                var before = body;
                var after = "";

                var start = body.IndexOf(startMarker, StringComparison.Ordinal);
                if (start > 0)
                {
                    // Build the updated body preserving the markers
                    before = body[..start];
                    var end = body.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
                    if (end > 0)
                        after = body[(end + endMarker.Length)..];
                }

                var newBody =
                    $"""
                    {before.Trim()}

                    {startMarker}

                    {newSection.Trim()}

                    {endMarker}

                    {after.Trim()}
                    """;

                if (!string.Equals(newBody, body, StringComparison.Ordinal) && payload.Repository is { } repo)
                {
                    if (payload.Release.Draft)
                    {
                        await github.Repository.Release.Delete(repo.Owner.Login, repo.Name, payload.Release.Id);

                        var tagName = payload.Release.TagName.StartsWith("unnamedtag") ? payload.Release.Name : payload.Release.TagName;
                        var release = await github.Repository.Release.Create(repo.Owner.Login, repo.Name,
                            new NewRelease(tagName)
                            {
                                Name = payload.Release.Name,
                                Body = newBody,
                                Draft = false,
                                Prerelease = payload.Release.Prerelease,
                                TargetCommitish = payload.Release.TargetCommitish
                            });

                        await CreateReleaseDiscussion(release, newBody, repo, cancellationToken);
                    }
                    else
                    {
                        var release = await github.Repository.Release.Edit(repo.Owner.Login, repo.Name, payload.Release.Id,
                            new ReleaseUpdate
                            {
                                Body = newBody
                            });

                        if (action == ReleaseAction.Published)
                            await CreateReleaseDiscussion(release, newBody, repo, cancellationToken);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                throw;
            }
        }

        // Enqueue release announcement to X/Twitter
        if (action == ReleaseAction.Published && !payload.Release.Draft && payload.Repository is { } announcementRepo)
        {
            var queue = queues.GetQueueClient(ReleaseAnnouncerFunctions.QueueName);
            try
            {
                await queue.SendMessageAsync(JsonSerializer.Serialize(new AnnounceRelease(
                    announcementRepo.Owner.Login,
                    announcementRepo.Name,
                    payload.Release.TagName,
                    payload.Release.Body ?? string.Empty,
                    payload.Release.HtmlUrl)), cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to queue release announcement");
            }
        }
    }

    async Task CreateReleaseDiscussion(Octokit.Release release, string content, Octokit.Webhooks.Models.Repository repo, CancellationToken cancellationToken)
    {
        if (config["SponsorLink:Account"] is string account)
        {
            try
            {
                var title = $"New release {repo.Owner.Login}/{repo.Name}@{release.TagName}";
                var body = $"{content}\n\n---\n\n🔗 [View Release]({release.HtmlUrl})";

                await CreateDiscussionAsync(account, ".github", title, body, cancellationToken);
            }
            catch (Exception e)
            {
                // Don't fail the whole webhook if discussion creation fails
                logger.LogWarning(e, "Failed to create discussion for release {Release}", release.TagName);
            }
        }
    }

    async Task CreateDiscussionAsync(string owner, string repo, string title, string body, CancellationToken cancellationToken)
    {
        // First, get the repository ID and discussion category ID
        var getRepoQuery = """
            query($owner: String!, $repo: String!) {
              repository(owner: $owner, name: $repo) {
                id
                discussionCategories(first: 10) {
                  nodes {
                    id
                    name
                  }
                }
              }
            }
            """;

        using var httpClient = httpFactory.CreateClient();

        // Add authentication header
        if (config["GitHub:Token"] is string token)
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SponsorLink-Webhook");
        }

        var queryResponse = await httpClient.PostAsJsonAsync("https://api.github.com/graphql", new
        {
            query = getRepoQuery,
            variables = new { owner, repo }
        }, cancellationToken);

        if (!queryResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get repository info for discussion creation: {Status}", queryResponse.StatusCode);
            return;
        }

        var queryResult = await queryResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        var repositoryId = queryResult?.RootElement
            .GetProperty("data")
            .GetProperty("repository")
            .GetProperty("id")
            .GetString();

        // Find the "Announcements" category
        var categoryId = queryResult?.RootElement
            .GetProperty("data")
            .GetProperty("repository")
            .GetProperty("discussionCategories")
            .GetProperty("nodes")
            .EnumerateArray()
            .FirstOrDefault(node =>
                node.TryGetProperty("name", out var nameProperty) &&
                nameProperty.GetString() == "Announcements")
            .GetProperty("id")
            .GetString();

        if (string.IsNullOrEmpty(repositoryId) || string.IsNullOrEmpty(categoryId))
        {
            logger.LogWarning("Could not find repository or Announcements category for {Owner}/{Repo}", owner, repo);
            return;
        }

        // Create the discussion
        var createDiscussionMutation = """
            mutation($repositoryId: ID!, $categoryId: ID!, $title: String!, $body: String!) {
              createDiscussion(input: {
                repositoryId: $repositoryId,
                categoryId: $categoryId,
                title: $title,
                body: $body
              }) {
                discussion {
                  id
                  url
                }
              }
            }
            """;

        var mutationResponse = await httpClient.PostAsJsonAsync("https://api.github.com/graphql", new
        {
            query = createDiscussionMutation,
            variables = new
            {
                repositoryId,
                categoryId,
                title,
                body
            }
        }, cancellationToken);

        if (mutationResponse.IsSuccessStatusCode)
        {
            var mutationResult = await mutationResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            var discussionUrl = mutationResult?.RootElement
                .GetProperty("data")
                .GetProperty("createDiscussion")
                .GetProperty("discussion")
                .GetProperty("url")
                .GetString();

            logger.LogInformation("Created discussion for release: {DiscussionUrl}", discussionUrl);
        }
        else
        {
            logger.LogWarning("Failed to create discussion: {Status}", mutationResponse.StatusCode);
        }
    }

    protected override async ValueTask ProcessIssueCommentWebhookAsync(WebhookHeaders headers, IssueCommentEvent payload, IssueCommentAction action, CancellationToken cancellationToken = default)
    {
        if (await issues.UpdateBacked(github, payload.Repository?.Id, (int)payload.Issue.Number) is null)
            // It was not an issue or it was not found.
            return;

        if (IsBot(payload.Sender))
            // Ignore comments from bots.
            return;

        try
        {
            using var activity = tracer.StartActivity("IssueComment");
            activity?.AddEvent(new ActivityEvent($"{activity?.OperationName}.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action)}"));

            activity?.SetTag("sender", payload.Sender?.Login);
            activity?.SetTag("repo", payload.Repository?.FullName);
            activity?.SetTag("issue", payload.Issue.Number.ToString());
            activity?.SetTag("comment", payload.Comment.Id.ToString());

            if (await manager.FindSponsorAsync(payload.Sender?.Login) is { } sponsor && sponsor.Kind != SponsorTypes.Team && sponsor.Kind != SponsorTypes.OpenSource)
            {
                if (sponsor.Tier.Meta.TryGetValue("tier", out var tier))
                    activity?.SetTag("sponsor", tier);
                else
                    activity?.SetTag("sponsor", "sponsor");

                if (action == IssueCommentAction.Created || action == IssueCommentAction.Edited)
                {
                    await notifier.PostAsync(new PushoverMessage
                    {
                        Title = $"🗨️ by {payload.Sender?.Login} as {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier ?? "")} sponsor",
                        Message = payload.Comment.Body,
                        Url = payload.Comment.HtmlUrl,
                        UrlTitle = $"View comment on issue #{payload.Issue.Number}",
                        //Priority = PushoverPriority.Normal
                    });
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            throw;
        }
    }

    protected override async ValueTask ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent payload, IssuesAction action, CancellationToken cancellationToken = default)
    {
        if (await issues.UpdateBacked(github, payload.Repository?.Id, (int)payload.Issue.Number) is not { } amount)
            // It was not an issue or it was not found.
            return;

        // Notify of changes to the issue body, if the issue was backed.
        if (IsBot(payload.Sender) && amount > 0)
        {
            await notifier.PostAsync(new PushoverMessage
            {
                Title = $"💸 Issue #{payload.Issue.Number} is backed!",
                Message = $"${amount} 👉 {payload.Issue.Title}\n@{payload.Repository?.FullName}#{payload.Issue.Number}",
                Url = payload.Issue.HtmlUrl,
                UrlTitle = $"View issue #{payload.Issue.Number}",
            });

            // But otherwise ignore comments/changes from bot.
            return;
        }

        try
        {
            using var activity = tracer.StartActivity("Issue");
            activity?.AddEvent(new ActivityEvent(action));

            activity?.SetTag("sender", payload.Sender?.Login);
            activity?.SetTag("repo", payload.Repository?.FullName);
            activity?.SetTag("issue", payload.Issue.Number.ToString());

            if (await manager.FindSponsorAsync(payload.Sender?.Login) is { } sponsor && sponsor.Kind != SponsorTypes.Team && sponsor.Kind != SponsorTypes.OpenSource)
            {
                if (sponsor.Tier.Meta.TryGetValue("tier", out var tier))
                    activity?.SetTag("sponsor", tier);
                else
                    activity?.SetTag("sponsor", "sponsor");

                if (!sponsor.Tier.Meta.TryGetValue("label", out var label))
                    label = "sponsor 💜";
                if (!sponsor.Tier.Meta.TryGetValue("color", out var color) || string.IsNullOrEmpty(color))
                    color = "#D4C5F9";

                // ensure Issue has the given label applied
                if (action == IssuesAction.Opened ||
                    action == IssuesAction.Edited ||
                    action == IssuesAction.Reopened ||
                    action == IssuesAction.Transferred)
                {
                    if (config["GitHub:Token"] is not { } ghtoken ||
                        payload.Issue.Labels.Any(x => x.Name == label) ||
                        payload.Repository is null)
                        return;

                    try
                    {
                        await github.Issue.Labels.Get(payload.Repository.Id, label);
                    }
                    catch (NotFoundException)
                    {
                        await github.Issue.Labels.Create(payload.Repository.Owner.Login, payload.Repository.Name, new NewLabel(label, color.TrimStart('#'))
                        {
                            Description = sponsor.Kind == SponsorTypes.Contributor ?
                                "Sponsor via contributions" :
                                tier != null && !"basic".Equals(tier, StringComparison.OrdinalIgnoreCase) && !"sponsor".Equals(tier, StringComparison.OrdinalIgnoreCase) ?
                                $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier)} Sponsor" :
                                "Sponsor"
                        });
                    }

                    // apply priority too if present
                    if (sponsor.Tier.Meta.TryGetValue("priority", out var priority))
                    {
                        if (priority == "")
                            priority = "priority";
                        else
                            priority = "priority:" + priority.Trim();

                        try
                        {
                            await github.Issue.Labels.Get(payload.Repository.Id, priority);
                        }
                        catch (NotFoundException)
                        {
                            await github.Issue.Labels.Create(payload.Repository.Owner.Login, payload.Repository.Name, new NewLabel(label, "EA4AAA"));
                        }

                        await github.Issue.Labels.AddToIssue(payload.Repository.Owner.Login, payload.Repository.Name, (int)payload.Issue.Number, [label, priority]);
                    }
                    else
                    {
                        await github.Issue.Labels.AddToIssue(payload.Repository.Owner.Login, payload.Repository.Name, (int)payload.Issue.Number, [label]);
                    }

                    await notifier.PostAsync(new PushoverMessage
                    {
                        Title = $"🐛 by {payload.Sender?.Login} as {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier ?? "")} sponsor",
                        Message = payload.Issue.Title,
                        Url = payload.Issue.HtmlUrl,
                        UrlTitle = $"View Issue #{payload.Issue.Number}",
                        //Priority = PushoverPriority.Normal
                    });
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            throw;
        }
    }

    static bool IsBot(Octokit.Webhooks.Models.User? user) =>
        user?.Type == UserType.Bot ||
        user?.Login.EndsWith("-bot") == true ||
        user?.Name?.EndsWith("bot]") == true ||
        user?.Name?.EndsWith("-bot") == true;

    [GeneratedRegex(@"\(https://github.com/(?<login>[^\)]+)\)")]
    private static partial Regex LoginExpr();
}
