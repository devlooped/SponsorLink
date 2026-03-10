using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devlooped.Sponsors;

/// <summary>
/// Client for posting tweets and threaded reply chains to the X/Twitter API v2.
/// </summary>
public class XClient(ILogger<XClient> logger, IOptions<AuthOptions> options, IHttpClientFactory httpClientFactory)
{
    const string TwitterApiUrl = "https://api.x.com/2/tweets";
    readonly HttpClient httpClient = httpClientFactory.CreateClient("x");

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<string?> PostTweetAndGetIdAsync(string text, string? replyToTweetId = null)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("X credentials not configured. Skipping post.");
            return null;
        }

        try
        {
            logger.LogInformation("Posting to X: {Preview}...",
                text.Length > 50 ? text[..50] : text);

            using var request = new HttpRequestMessage(HttpMethod.Post, TwitterApiUrl);
            var body = new XPostRequest { Text = text };
            if (!string.IsNullOrEmpty(replyToTweetId))
                body.Reply = new XReplyRequest { InReplyToTweetId = replyToTweetId };

            request.Content = JsonContent.Create(body);

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var tweetResponse = JsonSerializer.Deserialize<XPostResponse>(responseContent);
                var tweetId = tweetResponse?.Data?.Id;
                logger.LogInformation("Posted to X successfully. ID: {TweetId}", tweetId);
                return tweetId;
            }

            logger.LogError("Failed to post to X. Status: {StatusCode}, Response: {Response}",
                response.StatusCode, responseContent);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error posting to X");
            return null;
        }
    }

    public async Task<bool> PostThreadAsync(IReadOnlyList<string> posts)
    {
        if (posts == null || posts.Count == 0)
            return false;

        string? lastTweetId = null;

        for (var i = 0; i < posts.Count; i++)
        {
            var tweetId = await PostTweetAndGetIdAsync(posts[i], lastTweetId);
            if (tweetId == null)
            {
                logger.LogWarning("Thread post {Index}/{Total} failed. Stopping thread.", i + 1, posts.Count);
                return false;
            }
            lastTweetId = tweetId;

            if (i < posts.Count - 1)
                await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return true;
    }
}

public class XPostRequest
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("reply")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public XReplyRequest? Reply { get; set; }
}

public class XReplyRequest
{
    [JsonPropertyName("in_reply_to_tweet_id")]
    public required string InReplyToTweetId { get; set; }
}

public class XPostResponse
{
    [JsonPropertyName("data")]
    public XPostData? Data { get; set; }
}

public class XPostData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}
