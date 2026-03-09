using System.Security.Cryptography;
using System.Text;

namespace Devlooped.Sponsors;

/// <summary>
/// Generates OAuth 1.0a authorization headers for X/Twitter API requests.
/// </summary>
public class OAuth1Helper
{
    readonly string? consumerKey;
    readonly string? consumerSecret;
    readonly string? accessToken;
    readonly string? accessTokenSecret;

    public bool IsConfigured =>
        !string.IsNullOrEmpty(consumerKey) &&
        !string.IsNullOrEmpty(consumerSecret) &&
        !string.IsNullOrEmpty(accessToken) &&
        !string.IsNullOrEmpty(accessTokenSecret);

    public OAuth1Helper(ReleaseAnnouncementOptions options)
    {
        consumerKey = options.ApiKey?.Trim();
        consumerSecret = options.ApiSecret?.Trim();
        accessToken = options.AccessToken?.Trim();
        accessTokenSecret = options.AccessTokenSecret?.Trim();
    }

    public string GenerateAuthorizationHeader(string httpMethod, string url)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("OAuth1 credentials are not configured.");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");

        var oauthParams = new SortedDictionary<string, string>
        {
            { "oauth_consumer_key", consumerKey! },
            { "oauth_nonce", nonce },
            { "oauth_signature_method", "HMAC-SHA1" },
            { "oauth_timestamp", timestamp },
            { "oauth_token", accessToken! },
            { "oauth_version", "1.0" }
        };

        var parameterString = string.Join("&",
            oauthParams.Select(kvp => $"{PercentEncode(kvp.Key)}={PercentEncode(kvp.Value)}"));

        var signatureBaseString = $"{httpMethod.ToUpper()}&{PercentEncode(url)}&{PercentEncode(parameterString)}";
        var signingKey = $"{PercentEncode(consumerSecret!)}&{PercentEncode(accessTokenSecret!)}";

        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(signatureBaseString));
        var signature = Convert.ToBase64String(hash);

        oauthParams.Add("oauth_signature", signature);

        var headerParams = oauthParams.Select(kvp => $"{PercentEncode(kvp.Key)}=\"{PercentEncode(kvp.Value)}\"");
        return $"OAuth {string.Join(", ", headerParams)}";
    }

    static string PercentEncode(string value)
    {
        var encoded = Uri.EscapeDataString(value);
        return encoded
            .Replace("!", "%21")
            .Replace("*", "%2A")
            .Replace("'", "%27")
            .Replace("(", "%28")
            .Replace(")", "%29");
    }
}
