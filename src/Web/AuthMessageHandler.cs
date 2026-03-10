using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Devlooped;

public class AuthMessageHandler : DelegatingHandler
{
    readonly Lazy<AuthOptions> credentials;

    public AuthMessageHandler(AuthOptions credentials) => this.credentials = new Lazy<AuthOptions>(ValidateOptions(credentials));

    // Options are already validated by the AuthOptionsValidation
    public AuthMessageHandler(IOptionsMonitor<AuthOptions> options)
        => credentials = new Lazy<AuthOptions>(() => options.CurrentValue);

    // Options are already validated by the AuthOptionsValidation
    public AuthMessageHandler(IOptionsMonitor<AuthOptions> options, HttpMessageHandler innerHandler) : base(innerHandler)
        => credentials = new Lazy<AuthOptions>(() => options.CurrentValue);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SignRequest(request, credentials.Value);
        return base.SendAsync(request, cancellationToken);
    }

    static AuthOptions ValidateOptions(AuthOptions options)
    {
        if (AuthOptionsValidation.Validate(options) is { Failed: true } result)
            throw new OptionsValidationException(nameof(AuthOptions), typeof(AuthOptions), result.Failures);

        return options;
    }

    static void SignRequest(HttpRequestMessage request, AuthOptions credentials)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");

        var parameters = new Dictionary<string, string>
        {
            { "oauth_consumer_key", credentials.ConsumerKey },
            { "oauth_nonce", nonce },
            { "oauth_signature_method", "HMAC-SHA1" },
            { "oauth_timestamp", timestamp },
            { "oauth_token", credentials.AccessToken },
            { "oauth_version", "1.0" }
        };

        if (request.RequestUri?.Query != null)
        {
            var queryParams = request.RequestUri.Query.TrimStart('?')
                .Split('&')
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x.Split('='))
                .ToDictionary(x => x[0], x => x.Length > 1 ? x[1] : "");

            foreach (var param in queryParams)
            {
                parameters[param.Key] = param.Value;
            }
        }

        var signatureBase = GenerateSignatureBase(request, parameters);
        var signatureKey = $"{Uri.EscapeDataString(credentials.ConsumerSecret)}&{Uri.EscapeDataString(credentials.AccessTokenSecret)}";
        var signature = GenerateSignature(signatureBase, signatureKey);

        parameters["oauth_signature"] = signature;

        var authHeader = "OAuth " + string.Join(",", parameters
            .OrderBy(x => x.Key)
            .Select(x => $"{Uri.EscapeDataString(x.Key)}=\"{Uri.EscapeDataString(x.Value)}\""));

        request.Headers.Add("Authorization", authHeader);
    }

    static string GenerateSignatureBase(HttpRequestMessage request, Dictionary<string, string> parameters)
    {
        var method = request.Method.ToString().ToUpper();
        var uri = request.RequestUri!.GetLeftPart(UriPartial.Path);

        // OAuth 1.0a requires double-encoding of certain characters
        var sortedParams = parameters
            .OrderBy(x => x.Key)
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")
            .ToList();

        var signatureBase = $"{method}&{Uri.EscapeDataString(uri)}&{Uri.EscapeDataString(string.Join("&", sortedParams))}";
        return signatureBase;
    }

    static string GenerateSignature(string signatureBase, string signatureKey)
    {
        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signatureKey));
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(signatureBase));
        return Convert.ToBase64String(hash);
    }
}
