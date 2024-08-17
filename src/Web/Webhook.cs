using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.IssueComment;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Events.Sponsorship;

namespace Devlooped.Sponsors;

public class Webhook(SponsorsManager manager, IConfiguration config, IPushover notifier, ILogger<Webhook> logger) : WebhookEventProcessor
{
    static ActivitySource tracer = ActivityTracer.Source;

    protected override async Task ProcessSponsorshipWebhookAsync(WebhookHeaders headers, SponsorshipEvent payload, SponsorshipAction action)
    {
        using var activity = tracer.StartActivity("Sponsorship" + action);
        activity?.AddEvent(new ActivityEvent(action));
        manager.RefreshSponsors();
        await base.ProcessSponsorshipWebhookAsync(headers, payload, action);
    }

    protected override async Task ProcessIssueCommentWebhookAsync(WebhookHeaders headers, IssueCommentEvent payload, IssueCommentAction action)
    {
        try
        {
            await base.ProcessIssueCommentWebhookAsync(headers, payload, action);
            await IssueCommentWebhook(manager, notifier, payload, action);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            throw;
        }
    }

    protected override async Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent payload, IssuesAction action)
    {
        try
        {
            await base.ProcessIssuesWebhookAsync(headers, payload, action);
            await IssueWebhook(payload, action);
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
            throw;
        }
    }

    static async Task IssueCommentWebhook(SponsorsManager manager, IPushover notifier, IssueCommentEvent payload, IssueCommentAction action)
    {
        using var activity = tracer.StartActivity("IssueComment");
        activity?.AddEvent(new ActivityEvent(action));

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

    async Task IssueWebhook(IssuesEvent payload, IssuesAction action)
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

                var client = new GitHubClient(new ProductHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion))
                {
                    // Optional bot-token allows for a label updating by the bot
                    Credentials = new Credentials(config["GitHub:BotToken"] ?? config["GitHub:Token"])
                };

                try
                {
                    await client.Issue.Labels.Get(payload.Repository.Id, label);
                }
                catch (NotFoundException)
                {
                    await client.Issue.Labels.Create(payload.Repository.Owner.Login, payload.Repository.Name, new NewLabel(label, color.TrimStart('#'))
                    {
                        Description = sponsor.Kind == SponsorTypes.Contributor ?
                            "Sponsor via contributions" :
                            tier != null ?
                            $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier)} Sponsor" :
                            "Sponsor"
                    });
                }

                await client.Issue.Labels.AddToIssue(payload.Repository.Owner.Login, payload.Repository.Name, (int)payload.Issue.Number, [label]);

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
}
