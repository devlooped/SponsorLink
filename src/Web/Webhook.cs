using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.IssueComment;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Events.Release;
using Octokit.Webhooks.Events.Sponsorship;
using Octokit.Webhooks.Models;

namespace Devlooped.Sponsors;

public partial class Webhook(SponsorsManager manager, SponsoredIssues issues, IConfiguration config, IGitHubClient github, IPushover notifier, ILogger<Webhook> logger) : WebhookEventProcessor
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

                if (!string.Equals(newBody, body, StringComparison.Ordinal))
                {
                    // Update release body via GitHub API
                    var repo = payload.Repository;
                    if (repo is not null)
                    {
                        var update = new ReleaseUpdate
                        {
                            Body = newBody
                        };

                        await github.Repository.Release.Edit(repo.Owner.Login, repo.Name, payload.Release.Id, update);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                throw;
            }
        }

        await base.ProcessReleaseWebhookAsync(headers, payload, action);
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
