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
        logger.LogInformation("Processing sponsorship webhook: action={Action}, sponsor={Sponsor}, tier={Tier}, isOneTime={IsOneTime}",
            action, payload.Sponsorship.Sponsor.Login, payload.Sponsorship.Tier.Name, payload.Sponsorship.Tier.IsOneTime);

        using var activity = tracer.StartActivity("Sponsorship");
        activity?.AddEvent(new ActivityEvent($"{activity?.OperationName}.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action)}"));

        logger.LogDebug("Triggering sponsor cache refresh");
        manager.RefreshSponsors();

        if (action == SponsorshipAction.Created && payload.Sponsorship.Tier.IsOneTime)
        {
            logger.LogInformation("One-time sponsorship created by {Sponsor} (${Amount}/mo), adding to sponsored issues",
                payload.Sponsorship.Sponsor.Login, payload.Sponsorship.Tier.MonthlyPriceInDollars);

            await issues.AddSponsorship(
                payload.Sponsorship.Sponsor.Login,
                payload.Sponsorship.NodeId,
                payload.Sponsorship.Tier.MonthlyPriceInDollars);
        }
        else if (action == SponsorshipAction.Created)
        {
            logger.LogInformation("Recurring sponsorship created by {Sponsor}, no issue tracking needed", payload.Sponsorship.Sponsor.Login);
        }
        else
        {
            logger.LogDebug("Sponsorship action {Action} for {Sponsor} requires no additional processing", action, payload.Sponsorship.Sponsor.Login);
        }

        await base.ProcessSponsorshipWebhookAsync(headers, payload, action);
    }

    protected override async ValueTask ProcessReleaseWebhookAsync(WebhookHeaders headers, ReleaseEvent payload, ReleaseAction action, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing release webhook: action={Action}, repo={Repo}, tag={Tag}, draft={Draft}, sender={Sender}",
            action, payload.Repository?.FullName, payload.Release.TagName, payload.Release.Draft, payload.Sender?.Login);

        await base.ProcessReleaseWebhookAsync(headers, payload, action, cancellationToken);

        // No annoucement anywhere, no discussion, no sponsors, no nothing.
        if (ReleaseAnnouncer.HasSkipAnnounce(payload.Release.Body))
        {
            logger.LogInformation("Skipping release {Tag}: body contains skip-announce marker", payload.Release.TagName);
            return;
        }

        if (action == ReleaseAction.Deleted)
        {
            logger.LogDebug("Skipping sponsor injection for deleted release {Tag}", payload.Release.TagName);
        }
        else if (payload.Repository?.Name == "sandbox")
        {
            logger.LogDebug("Skipping sponsor injection for sandbox repository release {Tag}", payload.Release.TagName);
        }

        if (action != ReleaseAction.Deleted &&
            payload.Repository?.Name != "sandbox" &&
            // Don't re-process something we did ourselves by checking if the sender is the same as the authenticated
            // user used to make changes to the release.
            await github.User.Current() is { } user && payload.Sender?.Login != user.Login)
        {
            logger.LogDebug("Proceeding with sponsor injection for release {Tag} (sender={Sender}, authenticatedUser={User})",
                payload.Release.TagName, payload.Sender?.Login, user.Login);
            // fetch sponsors markdown from https://github.com/devlooped/sponsors/raw/refs/heads/main/sponsors.md
            // lookup for <!-- sponsors --> and <!-- /sponsors --> markers
            // replace that section in the release body with the markdown

            try
            {
                using var activity = tracer.StartActivity("Release");
                activity?.AddEvent(new ActivityEvent($"{activity?.OperationName}.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action)}"));

                var body = payload.Release.Body ?? string.Empty;
                if (body.Contains("<!-- nosponsors -->"))
                {
                    logger.LogInformation("Skipping sponsor section injection for release {Tag}: body contains nosponsors marker", payload.Release.TagName);
                    return;
                }

                const string startMarker = "<!-- sponsors -->";
                const string endMarker = "<!-- /sponsors -->";

                logger.LogDebug("Fetching sponsors markdown for release {Tag}", payload.Release.TagName);
                // Get sponsors markdown
                using var http = new HttpClient();
                var sponsorsMarkdown = await http.GetStringAsync("https://github.com/devlooped/sponsors/raw/refs/heads/main/sponsors.md", cancellationToken);
                if (string.IsNullOrWhiteSpace(sponsorsMarkdown))
                {
                    logger.LogWarning("Sponsors markdown was empty, skipping sponsor section injection for release {Tag}", payload.Release.TagName);
                    return;
                }

                var logins = LoginExpr().Matches(sponsorsMarkdown)
                    .Select(x => x.Groups["login"].Value)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => "@" + x)
                    .Distinct()
                    .ToList();

                logger.LogDebug("Found {Count} sponsors for release {Tag}: {Logins}", logins.Count, payload.Release.TagName, string.Join(", ", logins));

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
                    logger.LogDebug("Found existing sponsors section in release {Tag}, will replace it", payload.Release.TagName);
                    // Build the updated body preserving the markers
                    before = body[..start];
                    var end = body.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
                    if (end > 0)
                        after = body[(end + endMarker.Length)..];
                }
                else
                {
                    logger.LogDebug("No existing sponsors section in release {Tag}, will append new section", payload.Release.TagName);
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
                    logger.LogInformation("Release body changed for {Repo}/{Tag}, updating release", repo.FullName, payload.Release.TagName);

                    if (payload.Release.Draft)
                    {
                        logger.LogInformation("Release {Tag} is a draft — deleting and recreating as non-draft with sponsor section", payload.Release.TagName);
                        await github.Repository.Release.Delete(repo.Owner.Login, repo.Name, payload.Release.Id);

                        var tagName = payload.Release.TagName.StartsWith("unnamedtag") ? payload.Release.Name : payload.Release.TagName;
                        logger.LogDebug("Creating new release for {Repo} with tag {Tag}", repo.FullName, tagName);
                        var release = await github.Repository.Release.Create(repo.Owner.Login, repo.Name,
                            new NewRelease(tagName)
                            {
                                Name = payload.Release.Name,
                                Body = newBody,
                                Draft = false,
                                Prerelease = payload.Release.Prerelease,
                                TargetCommitish = payload.Release.TargetCommitish
                            });

                        logger.LogInformation("Created release {Url}, creating discussion", release.HtmlUrl);
                        await CreateReleaseDiscussion(release, newBody, repo, cancellationToken);
                    }
                    else
                    {
                        logger.LogDebug("Editing existing non-draft release {Tag} in {Repo} to add sponsor section", payload.Release.TagName, repo.FullName);
                        var release = await github.Repository.Release.Edit(repo.Owner.Login, repo.Name, payload.Release.Id,
                            new ReleaseUpdate
                            {
                                Body = newBody
                            });

                        if (action == ReleaseAction.Published)
                        {
                            logger.LogInformation("Release {Tag} was published, creating discussion", payload.Release.TagName);
                            await CreateReleaseDiscussion(release, newBody, repo, cancellationToken);
                        }
                        else
                        {
                            logger.LogDebug("Release {Tag} action was {Action}, skipping discussion creation", payload.Release.TagName, action);
                        }
                    }
                }
                else if (string.Equals(newBody, body, StringComparison.Ordinal))
                {
                    logger.LogDebug("Release {Tag} body unchanged after sponsor injection, skipping update", payload.Release.TagName);
                }
                else
                {
                    logger.LogDebug("Release {Tag} has no repository info, skipping update", payload.Release.TagName);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                throw;
            }
        }
        else if (action == ReleaseAction.Deleted)
        {
            // already logged above
        }
        else if (payload.Repository?.Name == "sandbox")
        {
            // already logged above
        }
        else if (await github.User.Current() is { } currentUser && payload.Sender?.Login == currentUser.Login)
        {
            logger.LogDebug("Skipping release {Tag}: sender {Sender} is the authenticated bot user (avoiding re-processing our own edits)", payload.Release.TagName, payload.Sender?.Login);
        }

        // Enqueue release announcement to X/Twitter
        if ((action == ReleaseAction.Published || ReleaseAnnouncer.HasForceAnnounce(payload.Release.Body)) &&
            !payload.Release.Draft &&
            payload.Repository is { } announcementRepo &&
            !string.IsNullOrEmpty(payload.Release.Body))
        {
            logger.LogInformation("Enqueueing release announcement for {Repo}/{Tag} (action={Action}, forceAnnounce={Force})",
                announcementRepo.FullName, payload.Release.TagName, action, ReleaseAnnouncer.HasForceAnnounce(payload.Release.Body));

            var queue = queues.GetQueueClient(ReleaseAnnouncerFunctions.QueueName);
            try
            {
                await queue.SendMessageAsync(JsonSerializer.Serialize(new AnnounceRelease(
                    announcementRepo.Owner.Login,
                    announcementRepo.Name,
                    payload.Release.TagName,
                    payload.Release.Body ?? string.Empty,
                    payload.Release.HtmlUrl)));

                logger.LogInformation("Release announcement queued for {Repo}/{Tag}", announcementRepo.FullName, payload.Release.TagName);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to queue release announcement");
            }
        }
        else
        {
            logger.LogDebug("Skipping release announcement queue for {Tag}: action={Action}, draft={Draft}, hasBody={HasBody}, forceAnnounce={Force}",
                payload.Release.TagName, action, payload.Release.Draft,
                !string.IsNullOrEmpty(payload.Release.Body),
                ReleaseAnnouncer.HasForceAnnounce(payload.Release.Body));
        }
    }

    async Task CreateReleaseDiscussion(Octokit.Release release, string content, Octokit.Webhooks.Models.Repository repo, CancellationToken cancellationToken)
    {
        if (config["SponsorLink:Account"] is string account)
        {
            logger.LogDebug("Creating discussion for release {Repo}/{Tag} in account {Account}", repo.FullName, release.TagName, account);
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
        else
        {
            logger.LogDebug("Skipping release discussion creation: SponsorLink:Account is not configured");
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
        logger.LogInformation("Processing issue comment webhook: action={Action}, repo={Repo}, issue={Issue}, comment={Comment}, sender={Sender}",
            action, payload.Repository?.FullName, payload.Issue.Number, payload.Comment.Id, payload.Sender?.Login);

        if (await issues.UpdateBacked(github, payload.Repository?.Id, (int)payload.Issue.Number) is null)
        {
            logger.LogDebug("Issue #{Issue} in repo {Repo} is not a backed issue or was not found, skipping comment processing",
                payload.Issue.Number, payload.Repository?.FullName);
            // It was not an issue or it was not found.
            return;
        }

        if (IsBot(payload.Sender))
        {
            logger.LogDebug("Skipping comment {Comment} on issue #{Issue}: sender {Sender} is a bot (type={Type}, login={Login})",
                payload.Comment.Id, payload.Issue.Number, payload.Sender?.Login, payload.Sender?.Type, payload.Sender?.Login);
            // Ignore comments from bots.
            return;
        }

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
                sponsor.Tier.Meta.TryGetValue("tier", out var tier);

                logger.LogInformation("Sender {Sender} is a sponsor (kind={Kind}, tier={Tier}) commenting on backed issue #{Issue}",
                    payload.Sender?.Login, sponsor.Kind, tier, payload.Issue.Number);

                if (sponsor.Tier.Meta.TryGetValue("tier", out tier))
                    activity?.SetTag("sponsor", tier);
                else
                    activity?.SetTag("sponsor", "sponsor");

                if (action == IssueCommentAction.Created || action == IssueCommentAction.Edited)
                {
                    logger.LogDebug("Sending Pushover notification for comment by sponsor {Sender} on issue #{Issue}", payload.Sender?.Login, payload.Issue.Number);
                    await notifier.PostAsync(new PushoverMessage
                    {
                        Title = $"🗨️ by {payload.Sender?.Login} as {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier ?? "")} sponsor",
                        Message = payload.Comment.Body,
                        Url = payload.Comment.HtmlUrl,
                        UrlTitle = $"View comment on issue #{payload.Issue.Number}",
                        //Priority = PushoverPriority.Normal
                    });
                }
                else
                {
                    logger.LogDebug("Issue comment action {Action} by sponsor {Sender} on issue #{Issue} requires no notification", action, payload.Sender?.Login, payload.Issue.Number);
                }
            }
            else if (await manager.FindSponsorAsync(payload.Sender?.Login) is { } nonActionableSponsor)
            {
                logger.LogDebug("Skipping comment on issue #{Issue}: sender {Sender} is a sponsor but kind={Kind} (Team or OpenSource sponsors are excluded)",
                    payload.Issue.Number, payload.Sender?.Login, nonActionableSponsor.Kind);
            }
            else
            {
                logger.LogDebug("Skipping comment on issue #{Issue}: sender {Sender} is not a sponsor", payload.Issue.Number, payload.Sender?.Login);
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
        logger.LogInformation("Processing issues webhook: action={Action}, repo={Repo}, issue={Issue}, sender={Sender}",
            action, payload.Repository?.FullName, payload.Issue.Number, payload.Sender?.Login);

        if (await issues.UpdateBacked(github, payload.Repository?.Id, (int)payload.Issue.Number) is not { } amount)
        {
            logger.LogDebug("Issue #{Issue} in repo {Repo} is not a backed issue or was not found, skipping",
                payload.Issue.Number, payload.Repository?.FullName);
            // It was not an issue or it was not found.
            return;
        }

        logger.LogDebug("Issue #{Issue} has backing amount ${Amount}", payload.Issue.Number, amount);

        // Notify of changes to the issue body, if the issue was backed.
        if (IsBot(payload.Sender) && amount > 0)
        {
            logger.LogInformation("Issue #{Issue} is backed (${Amount}), notifying via Pushover (sender is bot: {Sender})",
                payload.Issue.Number, amount, payload.Sender?.Login);

            await notifier.PostAsync(new PushoverMessage
            {
                Title = $"💸 Issue #{payload.Issue.Number} is backed!",
                Message = $"${amount} 👉 {payload.Issue.Title}\n@{payload.Repository?.FullName}#{payload.Issue.Number}",
                Url = payload.Issue.HtmlUrl,
                UrlTitle = $"View issue #{payload.Issue.Number}",
            });

            logger.LogDebug("Skipping further processing for bot sender {Sender} on issue #{Issue}", payload.Sender?.Login, payload.Issue.Number);
            // But otherwise ignore comments/changes from bot.
            return;
        }

        if (IsBot(payload.Sender))
        {
            logger.LogDebug("Skipping issue #{Issue}: sender {Sender} is a bot but amount is 0", payload.Issue.Number, payload.Sender?.Login);
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
                sponsor.Tier.Meta.TryGetValue("tier", out var tier);

                logger.LogInformation("Sender {Sender} is a sponsor (kind={Kind}, tier={Tier}) for issue #{Issue} action={Action}",
                    payload.Sender?.Login, sponsor.Kind, tier, payload.Issue.Number, action);

                if (sponsor.Tier.Meta.TryGetValue("tier", out tier))
                    activity?.SetTag("sponsor", tier);
                else
                    activity?.SetTag("sponsor", "sponsor");

                if (!sponsor.Tier.Meta.TryGetValue("label", out var label))
                    label = "sponsor 💜";
                if (!sponsor.Tier.Meta.TryGetValue("color", out var color) || string.IsNullOrEmpty(color))
                    color = "#D4C5F9";

                logger.LogDebug("Sponsor label={Label}, color={Color} for {Sender}", label, color, payload.Sender?.Login);

                // ensure Issue has the given label applied
                if (action == IssuesAction.Opened ||
                    action == IssuesAction.Edited ||
                    action == IssuesAction.Reopened ||
                    action == IssuesAction.Transferred)
                {
                    if (config["GitHub:Token"] is not { } ghtoken)
                    {
                        logger.LogWarning("Skipping label application for issue #{Issue}: GitHub:Token is not configured", payload.Issue.Number);
                        return;
                    }

                    if (payload.Issue.Labels.Any(x => x.Name == label))
                    {
                        logger.LogDebug("Issue #{Issue} already has label '{Label}', skipping label application", payload.Issue.Number, label);
                        return;
                    }

                    if (payload.Repository is null)
                    {
                        logger.LogWarning("Skipping label application for issue #{Issue}: repository info missing", payload.Issue.Number);
                        return;
                    }

                    try
                    {
                        await github.Issue.Labels.Get(payload.Repository.Id, label);
                        logger.LogDebug("Label '{Label}' already exists in repo {Repo}", label, payload.Repository.FullName);
                    }
                    catch (NotFoundException)
                    {
                        logger.LogInformation("Label '{Label}' not found in repo {Repo}, creating it", label, payload.Repository.FullName);
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

                        logger.LogDebug("Sponsor has priority metadata, applying labels [{Label}, {Priority}] to issue #{Issue}", label, priority, payload.Issue.Number);

                        try
                        {
                            await github.Issue.Labels.Get(payload.Repository.Id, priority);
                            logger.LogDebug("Priority label '{Priority}' already exists in repo {Repo}", priority, payload.Repository.FullName);
                        }
                        catch (NotFoundException)
                        {
                            logger.LogInformation("Priority label '{Priority}' not found in repo {Repo}, creating it", priority, payload.Repository.FullName);
                            await github.Issue.Labels.Create(payload.Repository.Owner.Login, payload.Repository.Name, new NewLabel(label, "EA4AAA"));
                        }

                        await github.Issue.Labels.AddToIssue(payload.Repository.Owner.Login, payload.Repository.Name, (int)payload.Issue.Number, [label, priority]);
                        logger.LogInformation("Applied labels [{Label}, {Priority}] to issue #{Issue} in {Repo}", label, priority, payload.Issue.Number, payload.Repository.FullName);
                    }
                    else
                    {
                        logger.LogDebug("No priority metadata for sponsor {Sender}, applying only label '{Label}' to issue #{Issue}", payload.Sender?.Login, label, payload.Issue.Number);
                        await github.Issue.Labels.AddToIssue(payload.Repository.Owner.Login, payload.Repository.Name, (int)payload.Issue.Number, [label]);
                        logger.LogInformation("Applied label '{Label}' to issue #{Issue} in {Repo}", label, payload.Issue.Number, payload.Repository.FullName);
                    }

                    logger.LogDebug("Sending Pushover notification for sponsor issue #{Issue} by {Sender}", payload.Issue.Number, payload.Sender?.Login);
                    await notifier.PostAsync(new PushoverMessage
                    {
                        Title = $"🐛 by {payload.Sender?.Login} as {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier ?? "")} sponsor",
                        Message = payload.Issue.Title,
                        Url = payload.Issue.HtmlUrl,
                        UrlTitle = $"View Issue #{payload.Issue.Number}",
                        //Priority = PushoverPriority.Normal
                    });
                }
                else
                {
                    logger.LogDebug("Issue #{Issue} action {Action} does not require label/notification processing", payload.Issue.Number, action);
                }
            }
            else if (await manager.FindSponsorAsync(payload.Sender?.Login) is { } nonActionableSponsor)
            {
                logger.LogDebug("Skipping issue #{Issue}: sender {Sender} is a sponsor but kind={Kind} (Team or OpenSource sponsors are excluded)",
                    payload.Issue.Number, payload.Sender?.Login, nonActionableSponsor.Kind);
            }
            else
            {
                logger.LogDebug("Skipping issue #{Issue}: sender {Sender} is not a sponsor", payload.Issue.Number, payload.Sender?.Login);
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
