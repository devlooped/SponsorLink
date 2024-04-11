using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace Devlooped.Sponsors;

/// <summary>
/// Returns a JWT or JSON manifest of the authenticated user's claims.
/// </summary>
class Sync(IConfiguration configuration, IHttpClientFactory httpFactory, SponsorsManager sponsors, RSA rsa, IWebHostEnvironment host, ILogger<Sync> logger)
{
    [Function("me")]
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
            request = req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"')),
            response = response.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value))
        })
        {
            StatusCode = (int)response.StatusCode
        };
    }

    /// <summary>
    /// Depending on the Accept header, returns a JWT or JSON manifest of the authenticated user's claims.
    /// </summary>
    [Function("sponsor")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (!configuration.TryGetClientId(logger, out var clientId))
            return new StatusCodeResult(500);

        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") && !string.IsNullOrEmpty(clientId))
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sponsor");

            logger.LogError("Ensure GitHub identity provider is configured for the functions app.");

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        var manifest = await sponsors.GetManifestAsync();

        if (manifest.ClientId != clientId)
        {
            logger.LogError("Ensure the GitHub identity provider client ID matches the one in the manifest.");
            return new StatusCodeResult(500);
        }

        var sponsor = await sponsors.GetSponsorAsync();

        if (sponsor == SponsorType.None ||
            principal.FindFirstValue("urn:github:login") is not string id)
            return new NotFoundObjectResult("You are not a sponsor");

        // TODO: add more claims in the future? tier, others?
        var claims = new List<Claim>
        {
            new("sub", id),
            new("sponsor", sponsor.ToString().ToLowerInvariant()),
        };

        // Allows the client to authenticate directly with the OAuth app if needed too.
        if (!string.IsNullOrEmpty(clientId))
            claims.Add(new("client_id", clientId));

        // Use shorthand JWT claim for email too. See https://www.iana.org/assignments/jwt/jwt.xhtml
        claims.AddRange(principal.Claims.Where(x => x.Type == ClaimTypes.Email).Select(x => new Claim("email", x.Value)));

        // We always respond authenticated requests either with a JWT or JSON, depending on the Accept header.
        if (req.Headers.Accept.Contains("application/jwt"))
        {
            return new ContentResult
            {
                Content = CreateJwt(rsa, manifest, claims),
                ContentType = "application/jwt",
                StatusCode = 200
            };
        }

        return new JsonResult(new
        {
            issuer = manifest.Issuer,
            audience = manifest.Audience,
            claims = principal.Claims
                // We already added the "email" claim above, so we skip it here.
                .Where(x => x.Type != ClaimTypes.Email)
                .Concat(claims)
                .GroupBy(x => x.Type)
                // Claims can have duplicates, so we group them and turn them into arrays, which is what JWT does too.
                .Select(g => new { g.Key, Value = (object)(g.Count() == 1 ? g.First().Value : g.Select(x => x.Value).ToArray()) })
                .ToDictionary(x => x.Key, x => x.Value),
#if DEBUG
            headers = req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"'))
#endif
        })
        {
            StatusCode = 200
        };
    }

    static string CreateJwt(RSA rsa, SponsorableManifest manifest, List<Claim> claims)
    {
        var signing = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        // Expire the first day of the next month
        var expiration = DateTime.UtcNow.AddMonths(1);

        // Use current time so they don't expire all at the same time
        expiration = new DateTime(expiration.Year, expiration.Month, 1,
            DateTime.UtcNow.Hour,
            DateTime.UtcNow.Minute,
            DateTime.UtcNow.Second,
            DateTime.UtcNow.Millisecond,
            DateTimeKind.Utc);

        claims.Insert(0, new("iss", manifest.Issuer));
        claims.Insert(1, new("aud", manifest.Audience));

        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: claims,
            expires: expiration,
            signingCredentials: signing
        ));

        return jwt;
    }

    public record GitHubProvider(bool Enabled, Registration Registration);
    public record Registration(string ClientId);
}