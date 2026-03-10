using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

/// <summary>
/// Summarizes release notes using an AI chat client, producing a ranked list of
/// emoji-prefixed feature lines suitable for threaded X/Twitter announcements.
/// </summary>
public partial class ReleaseSummarizer([FromKeyedServices("Grok")] IChatClient? chatClient, ILogger<ReleaseSummarizer> logger)
{
    const int MaxContentLength = 4000;
    const int MaxRetries = 3;

    static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool IsConfigured => chatClient != null;

    public async Task<ThreadPlan?> SummarizeReleaseAsync(string tagName, string markdownBody, string releaseUrl, CancellationToken cancellationToken = default)
    {
        if (chatClient == null)
        {
            logger.LogWarning("AI chat client not configured. Cannot summarize release.");
            return null;
        }

        var cleaned = PrepareContent(markdownBody);
        var prompt = BuildPrompt(tagName, cleaned);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt()),
            new(ChatRole.User, prompt)
        };

        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.Json
        };

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Requesting AI release summary for {Tag} (attempt {Attempt}/{Max})",
                    tagName, attempt, MaxRetries);

                var response = await chatClient.GetResponseAsync(messages, options);
                var json = StripCodeFences(response.Messages.LastOrDefault()?.Text?.Trim() ?? string.Empty);

                var plan = JsonSerializer.Deserialize<ThreadPlan>(json, jsonOptions);

                if (plan?.Items is not { Count: > 0 })
                {
                    logger.LogWarning("AI returned empty plan for {Tag} (attempt {Attempt})", tagName, attempt);
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                        continue;
                    }
                    return null;
                }

                if (plan.TotalCount <= 0)
                    plan.TotalCount = plan.Items.Count;

                logger.LogInformation("Generated release summary: {Total} total, {Items} ranked items for {Tag}",
                    plan.TotalCount, plan.Items.Count, tagName);

                return plan;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Timed out generating summary for {Tag} (attempt {Attempt})", tagName, attempt);
                if (attempt < MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                    continue;
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating summary for {Tag} (attempt {Attempt})", tagName, attempt);
                if (attempt < MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                    continue;
                }
                return null;
            }
        }

        return null;
    }

    static string GetSystemPrompt() =>
        """
        You are an expert at analyzing software release notes and creating concise, engaging summaries for social media.

        Your task is to:
        1. Identify the most exciting and impactful features or changes
        2. Format them as concise, emoji-prefixed lines
        3. Rank items by importance (most exciting first)
        4. NEVER include user names, contributor names, or issue/PR numbers
        5. Focus ONLY on features, fixes, and improvements

        Emoji guidelines:
        - ✨ for new features
        - ⚡ for performance improvements
        - 🐛 for bug fixes
        - 🔒 for security updates
        - 📖 for documentation
        - 🎉 for major milestones

        Keep the tone exciting and developer-friendly.
        """;

    static string BuildPrompt(string tagName, string content) =>
        $$"""
        Extract and rank all notable features from this release: {{tagName}}

        Release Content (markdown):
        {{content}}

        Return ALL items ranked by importance. Respond with JSON only. Example:
        {
          "totalCount": 8,
          "items": ["✨ New interactive setup flow", "⚡ 3x faster indexing", "🐛 Fixed auth token refresh", "🔒 Hardened credential storage"]
        }

        Requirements:
        - Each item should be a concise emoji-prefixed description (30-60 chars)
        - NEVER include contributor names, @mentions, or PR/issue numbers
        - Focus ONLY on what changed, not who contributed
        - Include ALL notable items from the release
        - totalCount should reflect the total number of notable changes
        """;

    static string PrepareContent(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        // Remove "New Contributors" and "Full Changelog" sections
        var cutoff = markdown.IndexOf("## New Contributors", StringComparison.OrdinalIgnoreCase);
        if (cutoff < 0)
            cutoff = markdown.IndexOf("**New Contributors**", StringComparison.OrdinalIgnoreCase);
        if (cutoff < 0)
            cutoff = markdown.IndexOf("**Full Changelog**", StringComparison.OrdinalIgnoreCase);
        if (cutoff > 0)
            markdown = markdown[..cutoff];

        // Remove contributor references like "by @user in #123"
        markdown = ContributorPattern().Replace(markdown, " ");

        // Cap length
        if (markdown.Length > MaxContentLength)
            markdown = markdown[..MaxContentLength] + "...[truncated]";

        return markdown.Trim();
    }

    static string StripCodeFences(string text)
    {
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0)
                text = text[(firstNewline + 1)..];
        }
        if (text.EndsWith("```"))
            text = text[..^3];

        return text.Trim();
    }

    [GeneratedRegex(@"(?:by\s+@\S+\s*(?:in\s+)?)?(?:https?://\S+/pull/\d+|#\d+)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ContributorPattern();
}

/// <summary>
/// Represents an AI-generated ranked list of features for thread assembly.
/// </summary>
public class ThreadPlan
{
    public int TotalCount { get; set; }
    public List<string> Items { get; set; } = [];
}
