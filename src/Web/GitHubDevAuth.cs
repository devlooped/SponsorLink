using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

#if DEBUG
class GitHubDevAuth(IConfiguration configuration, IHttpClientFactory httpFactory, IWebHostEnvironment host, ILogger<GitHubDevAuth> logger)
{
    record AuthCode(string? access_token, string? token_type, string? scope, AuthError? error, string? error_description, int? interval);
    enum AuthError
    {
        authorization_pending,
        slow_down,
        expired_token,
        unsupported_grant_type,
        incorrect_client_credentials,
        incorrect_device_code,
        access_denied,
        device_flow_disabled
    }

    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<AuthError>() },
        WriteIndented = true
    };

    /// <summary>
    /// This helper callback function makes sure we can have the same callback URL format in the dev GH OAuth app 
    /// and the real production app (save for the host name, via ngrok). 
    /// It's never compiled and deployed to Azure functions, though, which already provides this via EasyAuth.
    /// </summary>
    [Function("github_callback")]
    public async Task<HttpResponseData> CallbackAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".auth/login/github/callback")] HttpRequestData request, string state)
    {
        if (!configuration.TryGetClientId(logger, out var clientId))
            return request.CreateResponse(HttpStatusCode.InternalServerError);

        var redirectUrl = state?.StartsWith("redir=") == true ? state[6..] : "/me";
        var response = request.CreateResponse(HttpStatusCode.TemporaryRedirect);
        response.Headers.Add("Location", redirectUrl);

        var code = request.Query["code"]?.ToString();
        var clientSecret = configuration["GitHub:ClientSecret"];
        if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(clientSecret))
        {
            using var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // We construct the url again within the loop since we might have restarted the flow after expiration.
            var url = $"https://github.com/login/oauth/access_token?client_id={clientId}&client_secret={clientSecret}&code={code}";
            var auth = await (await http.PostAsync(url, null)).Content.ReadFromJsonAsync<AuthCode>(options);

            if (auth?.error != null)
                logger.LogWarning("{error}: {description}", auth.error, auth.error_description);

            if (auth?.access_token is not null)
            {
                response.Cookies.Append(new HttpCookie("access_token", auth.access_token)
                {
                    Path = "/",
                    SameSite = SameSite.ExplicitNone,
                    Secure = true,
                });
            }
        }

        return response;
    }

    [Function("github_me")]
    public IActionResult AuthMe([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".auth/me")] HttpRequestData request)
    {
        if (!configuration.TryGetClientId(logger, out var clientId))
            return new StatusCodeResult(500);

        var cookie = request.Cookies.FirstOrDefault(x => x.Name == "access_token");
        if (cookie == null)
            return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&state=redir=/.auth/me");

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
            return new StatusCodeResult(401);

        // Simulate as close as possible the behavior of the easyauth endpoint.
        return new ObjectResult(new
        {
            access_token = cookie.Value,
            provider_name = "github",
            user_claims = principal.Claims.Select(x => new { typ = x.Type, val = x.Value }),
            user_id = principal.FindFirstValue(ClaimTypes.Email),
        });
    }
}
#endif