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
using Octokit.Webhooks.Events.Sponsorship;

namespace Devlooped.Sponsors;

public partial class Webhook(SponsorsManager manager, SponsoredIssues issues, IConfiguration config, IGitHubClient github, IPushover notifier, ILogger<Webhook> logger) : WebhookEventProcessor
{
    static ActivitySource tracer = ActivityTracer.Source;

    protected override async Task ProcessSponsorshipWebhookAsync(WebhookHeaders headers, SponsorshipEvent payload, SponsorshipAction action)
    {
        using var activity = tracer.StartActivity("Sponsorship");
        activity?.AddEvent(new ActivityEvent($"{activity?.OperationName}.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action)}"));
        manager.RefreshSponsors();

        if (action == SponsorshipAction.Created && payload.Sponsorship.Tier.IsOneTime)
        {
            await issues.AddSponsorshipAsync(
                payload.Sponsorship.Sponsor.Login,
                payload.Sponsorship.NodeId,
                payload.Sponsorship.Tier.MonthlyPriceInDollars);
        }

        await base.ProcessSponsorshipWebhookAsync(headers, payload, action);
    }

    protected override async Task ProcessIssueCommentWebhookAsync(WebhookHeaders headers, IssueCommentEvent payload, IssueCommentAction action)
    {
        await EnsureBackerAsync(payload.Repository?.Id, payload.Issue.Number, payload.Issue.Body);

        try
        {
            using var activity = tracer.StartActivity("IssueComment");
            activity?.AddEvent(new ActivityEvent($"{activity?.OperationName}.{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(action)}"));
            
            activity?.SetTag("sender", payload.Sender?.Login);
            activity?.SetTag("repo", payload.Repository?.FullName);
            activity?.SetTag("issue", payload.Issue.Number.ToString());
            activity?.SetTag("comment", payload.Comment.Id.ToString());

            if (await manager.FindSponsorAsync(payload.Sender?.Login) is { } sponsor && sponsor.Kind != SponsorTypes.Team)
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
                        Url = payload.Comment.Url,
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

    protected override async Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent payload, IssuesAction action)
    {
        // Ensure backer amount badge is present/updated
        await EnsureBackerAsync(payload.Repository?.Id, payload.Issue.Number, payload.Issue.Body);
        
        try
        {
            using var activity = tracer.StartActivity("Issue");
            activity?.AddEvent(new ActivityEvent(action));

            activity?.SetTag("sender", payload.Sender?.Login);
            activity?.SetTag("repo", payload.Repository?.FullName);
            activity?.SetTag("issue", payload.Issue.Number.ToString());

            if (await manager.FindSponsorAsync(payload.Sender?.Login) is { } sponsor && sponsor.Kind != SponsorTypes.Team)
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
                        Url = payload.Issue.Url,
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

    async Task EnsureBackerAsync(long? repository, long number, string? body)
    {
        if (repository is null || body is null)
            return;

        var amount = await issues.BackedAmount(repository.Value, (int)number);
        var updated = await issues.UpdateIssueBody(repository.Value, (int)number, body);

        if (updated != body)
        {
            var issue = new IssueUpdate
            {
                Body = updated,
            };
            await github.Issue.Update(repository.Value, (int)number, issue);
        }
    }
}
