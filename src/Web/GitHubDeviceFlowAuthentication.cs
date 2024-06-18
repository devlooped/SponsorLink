using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using GitCredentialManager;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

public static class GitHubDeviceFlowAuthenticationExtensions
{
    /// <summary>
    /// Parses existing app service authentication headers and sets them as 
    /// <see cref="FunctionContext.Features"/>.Set(<see cref="ClaimsPrincipal"/>) and 
    /// <see cref="FunctionContext.Features"/>.Set(<see cref="AccessToken"/>) if present.
    /// </summary>
    public static IFunctionsWorkerApplicationBuilder UseGitHubDeviceFlowAuthentication(this IFunctionsWorkerApplicationBuilder builder)
        => builder.UseMiddleware<GitHubDeviceFlowMiddleware>();

    class GitHubDeviceFlowMiddleware(IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<GitHubDeviceFlowMiddleware> logger) : IFunctionsWorkerMiddleware
    {
        static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter<AuthError>() },
            WriteIndented = true
        };

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            if (context.Features.Get<ClaimsPrincipal>() is not null)
            {
                // Already set by some other component.
                await next(context);
                return;
            }

            var req = await context.GetHttpRequestDataAsync();
            // If no request data or already authenticated (i.e. via CLI), nothing to do.
            if (req is null || req.Headers.Any(x => x.Key == "Authorization"))
            {
                await next(context);
                return;
            }

            var cookie = req.Cookies.FirstOrDefault(x => x.Name == "access_token");
            if (cookie != null)
            {
                req.Headers.Add("Authorization", $"Bearer {cookie.Value}");
                await next(context);
                return;
            }

            if (!configuration.TryGetClientId(logger, out var clientId))
            {
                await next(context);
                return;
            }

            using var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Try retrieving a cached token, like we would from the CLI
            var store = CredentialManager.Create("com.devlooped");
            var creds = store.Get("https://github.com", clientId);
            if (creds != null)
            {
                var request = new HttpRequestMessage(HttpMethod.Head, "https://api.github.com/user");
                request.Headers.Authorization = new("Bearer", creds.Password);
                if (await http.SendAsync(request) is HttpResponseMessage { IsSuccessStatusCode: true } response)
                {
                    req.Headers.Add("Authorization", $"Bearer {creds.Password}");
                    await next(context);
                    return;
                }
                else
                {
                    // If the token is invalid, remove it from the store.
                    store.Remove("https://github.com", clientId);
                }
            }

            var codeUrl = $"https://github.com/login/device/code?client_id={clientId}&scope=read:user,user:email,read:org";
            var resp = await http.PostAsync(codeUrl, null);

            if (resp is { IsSuccessStatusCode: false })
            {
                logger.LogWarning("Failed to start device flow");
                await next(context);
                return;
            }

            var json = await resp.Content.ReadAsStringAsync();
            var auth = JsonSerializer.Deserialize<Auth>(json, options);
            if (auth is null)
            {
                logger.LogWarning("Failed to start device flow");
                await next(context);
                return;
            }

            // Render the auth response as JSON to console, user should copy the code to paste on the URL in the browser
            logger.LogWarning("Navigate to {uri}", auth.verification_uri);
            logger.LogWarning("Then enter code: {code}", auth.user_code);

            AuthCode? code = default;
            do
            {
                if (code?.error != null)
                    logger.LogWarning("{error}: {description}", code.error, code.error_description);

                // Be gentle with the backend, wait for the interval before polling again.
                await Task.Delay(TimeSpan.FromSeconds(auth!.interval));

                // We construct the url again within the loop since we might have restarted the flow after expiration.
                var url = $"https://github.com/login/oauth/access_token?client_id={clientId}&device_code={auth.device_code}&grant_type=urn:ietf:params:oauth:grant-type:device_code";

                code = await (await http.PostAsync(url, null)).Content.ReadFromJsonAsync<AuthCode>(options);

                if (code!.error == AuthError.slow_down && code.interval is int interval)
                {
                    // This is per the docs, we should slow down the polling.
                    await Task.Delay(TimeSpan.FromSeconds(interval));
                }
                else if (code.error == AuthError.expired_token)
                {
                    // We need an entirely new code, start over.
                    auth = await (await http.PostAsync(codeUrl, null)).Content.ReadFromJsonAsync<Auth>();
                }
                else if (code.error == AuthError.authorization_pending)
                {
                    logger.LogWarning("Navigate to {uri}", auth.verification_uri);
                    logger.LogWarning("Then enter code: {code}", auth.user_code);
                }

                // Continue while we have an error, meaning the code has not been authorized yet.
            } while (code.error != null);

            if (code.access_token is not null)
            {
                // Persist the token for use across restarts, just like the CLI
                store.AddOrUpdate("https://github.com", clientId, code.access_token);
                req.Headers.Add("Authorization", $"Bearer {code.access_token}");
                // Should now perform authentication using the access token as if accessed by the CLI.
                await next(context);

                // If we have a json result, we can update the response and set the cookie.
                // Otherwise, we'd need to duplicate a lot of the logic in the next middleware.
                if (context.GetInvocationResult().Value is IActionResult result)
                {
                    var response = req.CreateResponse();
                    response.Cookies.Append("access_token", code.access_token);

                    switch (result)
                    {
                        case JsonResult jr:
                            if (jr.Value is not null)
                                await response.WriteAsJsonAsync(jr.Value, jr.ContentType ?? "application/json; charset=utf-8",
                                    (HttpStatusCode)(jr.StatusCode ?? 200));
                            else if (jr.StatusCode is not null)
                                response.StatusCode = (HttpStatusCode)jr.StatusCode;
                            break;
                        case ContentResult cr:
                            if (cr.StatusCode is not null)
                                response.StatusCode = (HttpStatusCode)cr.StatusCode;
                            if (cr.ContentType is not null)
                                response.Headers.Add("Content-Type", cr.ContentType);
                            if (cr.Content is not null)
                                await response.WriteStringAsync(cr.Content);
                            break;
                        case StatusCodeResult sr:
                            response.StatusCode = (HttpStatusCode)sr.StatusCode;
                            break;
                        default:
                            return;
                    }

                    context.GetInvocationResult().Value = response;
                }

                return;
            }

            await next(context);
        }
    }

    record Auth(string device_code, string user_code, string verification_uri, int interval, int expires_in);
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
}