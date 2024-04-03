using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true} principal)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") &&
                configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
            {
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/me");
            }

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        using var http = httpFactory.CreateClient("sponsor");
        var response = await http.GetAsync("https://api.github.com/user");

        var emails = await http.GetFromJsonAsync<JsonArray>("https://api.github.com/user/emails");
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body?.Add("emails", emails);

        return new JsonResult(new
        {
            body,
            claims = principal.Claims.GroupBy(x => x.Type)
                .Select(g => new { g.Key, Value = (object)(g.Count() == 1 ? g.First().Value : g.Select(x => x.Value).ToArray()) })
                .ToDictionary(x => x.Key, x => x.Value),
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
        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") &&
                configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
            {
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&scope=read:user%20read:org%20user:email&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sponsor");
            }

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        var manifest = await sponsors.GetManifestAsync();
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
            // Claims can have duplicates, so we group them and turn them into arrays, which is what JWT does too.
            claims = principal.Claims.GroupBy(x => x.Type)
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