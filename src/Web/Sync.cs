using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SharpYaml.Serialization;

namespace Devlooped.Sponsors;

/// <summary>
/// Returns a JWT or JSON manifest of the authenticated user's claims.
/// </summary>
class Sync(IConfiguration configuration, IHttpClientFactory httpFactory, SponsorsManager sponsors, RSA rsa)
{
    [Function("me")]
    public async Task<IActionResult> UserAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true})
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") &&
                configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
            {
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sync");
            }

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        using var http = httpFactory.CreateClient("sponsor");
        var response = await http.GetAsync("https://api.github.com/user");

        return new JsonResult(new
        {
            body = await response.Content.ReadFromJsonAsync<JsonElement>(),
            request = req.Headers.ToDictionary(x => x.Key, x => x.Value.ToString().Trim('"')),
            response = response.Headers.ToDictionary(x => x.Key, x => x.Value?.ToString()?.Trim('"'))
        })
        {
            StatusCode = 200
        };
    }

    /// <summary>
    /// Depending on the Accept header, returns a JWT or JSON manifest of the authenticated user's claims.
    /// </summary>
    [Function("sync")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") &&
                configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
            {
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sync");
            }

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        var manifest = await sponsors.GetManifestAsync();
        var sponsor = await sponsors.GetSponsorAsync();

        if (sponsor == SponsorType.None)
            return new NotFoundObjectResult("You are not a sponsor");

        // TODO: add more claims in the future? tier, others?
        var claims = new List<Claim>
        {
            new("sponsor", sponsor.ToString().ToLowerInvariant())
        };

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
            claims = claims.ToDictionary(x => x.Type, x => x.Value),
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

        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: manifest.Issuer,
            audience: manifest.Audience,
            expires: expiration,
            signingCredentials: signing,
            claims: claims
        ));

        return jwt;
    }
}