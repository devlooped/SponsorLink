using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

/// <summary>
/// Returns a JWT or JSON manifest of the authenticated user's claims.
/// </summary>
partial class SponsorLink(IConfiguration configuration, IHttpClientFactory httpFactory, SponsorsManager sponsors, RSA rsa, IOptions<SponsorLinkOptions> options, IWebHostEnvironment host, ILogger<SponsorLink> logger)
{
    SponsorLinkOptions options = options.Value;

    /// <summary>
    /// Helper to visualize the user's claims and the request/response headers as available to the backend.
    /// </summary>
    [Function("user")]
    public async Task<IActionResult> UserAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (!configuration.TryGetClientId(logger, out var clientId))
            return new StatusCodeResult(500);

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") && !string.IsNullOrEmpty(clientId))
            {
                var redirectHost = host.IsDevelopment() ?
                    "donkey-emerging-civet.ngrok-free.app" : req.Headers["Host"].ToString();

                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&redirect_uri=https://{redirectHost}/.auth/login/github/callback&state=redir=/me");
            }

            logger.LogError("Ensure GitHub identity provider is configured for the functions app.");

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        using var http = httpFactory.CreateClient("sponsor");
        var response = await http.GetAsync("https://api.github.com/user");

        var emails = await http.GetFromJsonAsync<JsonArray>("https://api.github.com/user/emails");
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body?.Add("emails", emails);

        // Claims can have duplicates, so we group them and turn them into arrays, which is what JWT does too.
        var claims = principal.Claims.GroupBy(x => x.Type)
            .Select(g => new { g.Key, Value = (object)(g.Count() == 1 ? g.First().Value : g.Select(x => x.Value).ToArray()) })
            .ToDictionary(x => x.Key, x => x.Value);

        // Allows the client to authenticate directly with the OAuth app if needed too.
        if (!string.IsNullOrEmpty(clientId))
            claims["client_id"] = clientId;

        return new JsonResult(new
        {
            body,
            claims,
            request = host.IsDevelopment() ? req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"')) : null,
            response = host.IsDevelopment() ? response.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value)) : null
        })
        {
            StatusCode = (int)response.StatusCode,
            SerializerSettings = JsonOptions.Default
        };
    }

    /// <summary>
    /// Returns the sponsorable manifest from <see cref="SponsorsManager.GetRawManifestAsync"/>.
    /// </summary>
    [Function("jwt")]
    public async Task<IActionResult> GetManifest([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var manifest = await sponsors.GetManifestAsync();
        if (!rsa.ThumbprintEquals(manifest.SecurityKey))
        {
            logger.LogError("Configured private key '{option}' does not match the manifest public key.", $"SponsorLink:{nameof(SponsorLinkOptions.PrivateKey)}");
            return new StatusCodeResult(500);
        }

        return new ContentResult()
        {
            Content = await sponsors.GetRawManifestAsync(),
            ContentType = "text/plain",
            StatusCode = 200,
        };
    }

    /// <summary>
    /// Returns the sponsorable public key in JWK format.
    /// </summary>
    [Function("jwk")]
    public async Task<IActionResult> GetPublicKey([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var manifest = await sponsors.GetManifestAsync();

        return new ContentResult()
        {
            Content = JsonSerializer.Serialize(JsonWebKeyConverter.ConvertFromSecurityKey(manifest.SecurityKey), JsonOptions.JsonWebKey),
            ContentType = "application/json",
            StatusCode = 200,
        };
    }

    /// <summary>
    /// Depending on the Accept header, returns a JWT or JSON manifest of the authenticated user's claims.
    /// </summary>
    [Function("me-get")]
    public async Task<IActionResult> SyncAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req)
    {
        if (!configuration.TryGetClientId(logger, out var clientId))
            return new StatusCodeResult(500);

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") && !string.IsNullOrEmpty(clientId))
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/me");

            logger.LogError("Ensure GitHub identity provider is configured for the functions app.");

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        var manifest = await sponsors.GetManifestAsync();

        // When running localhost, we use a different GH app (and therefore client ID) 
        // which redirects to localhost:4242. Since GH apps cannot provide more than one 
        // redirect URL, this is unavoidable as of now. 
        // So if we're running in a dev environment, we need to adjust the client ID 
        // that's also used in the GitHubAuthExtensions
        if (host.IsDevelopment())
            manifest.ClientId = clientId;

        // Shield against misconfiguration that can cause unpredictable behavior.
        if (manifest.ClientId != clientId)
        {
            logger.LogError("Ensure the configured GitHub identity provider client ID matches the one in the manifest.");
            return new StatusCodeResult(500);
        }

        if (await sponsors.GetSponsorClaimsAsync() is not { } claims)
            return new NotFoundObjectResult("You are not a sponsor");

        var jwt = manifest.Sign(claims, rsa);

        // We always respond authenticated requests either with a JWT or JSON, depending on the Accept header.
        if (req.Headers.Accept.Contains("application/jwt"))
        {
            return new ContentResult
            {
                Content = jwt,
                ContentType = "application/jwt",
                StatusCode = 200
            };
        }

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        return new ContentResult
        {
            Content = token.Payload.SerializeToJson(),
            ContentType = "application/json",
            StatusCode = 200
        };
    }

    [Function("me-delete")]
    public IActionResult Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "me")] HttpRequest req)
    {
        if (!configuration.TryGetClientId(logger, out _))
            return new StatusCodeResult(500);

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true })
            return new UnauthorizedResult();

        logger.LogInformation("We don't persist anything, so there's nothing to delete :)");

        return new OkResult();
    }
}