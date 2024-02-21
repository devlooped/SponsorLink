using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SharpYaml.Serialization;

namespace Devlooped.SponsorLink;


/// <summary>
/// Returns a JWT or JSON manifest of the authenticated user's claims.
/// </summary>
public class Sync(
    ILogger<Sync> logger, IConfiguration configuration, 
    IHttpClientFactory httpFactory, IMemoryCache cache,
    RSA rsa)
{
    static readonly Serializer serializer = new(new SerializerSettings
    {
        NamingConvention = new CamelCaseNamingConvention()
    });

    class Manifest
    {
        public required string Issuer { get; set; }
        public required string Audience { get; set; }
        [YamlMember("pub")]
        public required string PublicKey { get; set; }
    }

    //public IActionResult Refresh([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    //{

    //}

    /// <summary>
    /// Depending on the Accept header, returns a JWT or JSON manifest of the authenticated user's claims.
    /// </summary>
    [Function("sync")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, FunctionContext context)
    {
        var feature = context.Features.Get<ClaimsFeature>();
        var principal = feature?.Principal ?? req.HttpContext.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            // Implement manual auto-redirect to GitHub, since we cannot turn it on in the portal
            // or the token-based principal population won't work.
            // Never redirect requests for JWT, as they are likely from a CLI or other non-browser client.
            if (!req.Headers.Accept.Contains("application/jwt") &&
                configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"] is { Length: > 0 } clientId)
            {
                return new RedirectResult($"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri=https://{req.Headers["Host"]}/.auth/login/github/callback&state=redir=/sync");
            }

            // Otherwise, just 401
            return new UnauthorizedResult();
        }

        if (!cache.TryGetValue<Manifest>(typeof(Manifest), out var manifest) || manifest is null)
        {
            // Populate manifest
            if (configuration["SPONSORLINK_ACCOUNT"] is not { Length: > 0 } account)
            {
                // Auto-discovery by fetching from [user]/.github/sponsorlink.yml
                using var gh = httpFactory.CreateClient("sponsorable");
                var data = await gh.PostAsJsonAsync("graphql", new
                {
                    query = "query { viewer { login } }"
                });

                if (!data.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to fetch sponsorable user: {StatusCode} {Reason}", data.StatusCode, await data.Content.ReadAsStringAsync());
                    return new StatusCodeResult(500);
                }

                var viewer = await data.Content.ReadFromJsonAsync<JsonElement>();
                account = viewer.GetProperty("data").GetProperty("viewer").GetProperty("login").GetString();
            }

            var url = $"https://github.com/{account}/.github/raw/main/sponsorlink.yml";

            //NOTE: it should be a public URL
            using var http = httpFactory.CreateClient();
            var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to retrieve manifest from {Url}: {StatusCode} {Reason}", url, (int)response.StatusCode, await response.Content.ReadAsStringAsync());
                return new StatusCodeResult(500);
            }

            var yaml = await response.Content.ReadAsStringAsync();
            manifest = serializer.Deserialize<Manifest>(yaml);

            if (manifest is null)
            {
                logger.LogError("Failed to deserialize YAML manifest from {Url}", url);
                return new StatusCodeResult(500);
            }

            if (manifest.Audience == null)
            {
                // Audience defaults to the manifest url user/org
                manifest.Audience = new Uri(url).Segments[1].Trim('/');
            }

            manifest = cache.Set(typeof(Manifest), manifest, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
        }

        var claims = principal.Claims;
        // Subject will be the authenticated user id: https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.2
        if (principal.FindFirstValue("urn:github:id") is not { Length: > 0 } subject)
        {
            logger.LogError("Missing required claim 'urn:github:id'");
            return new StatusCodeResult(500);
        }

        // Prepend the shorthand sub claim
        claims = new[] { new Claim("sub", subject) }.Concat(claims);

        // We always respond authenticated requests either with a JWT or JSON, depending on the Accept header.
        if (req.Headers.Accept.Contains("application/jwt"))
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
                signingCredentials: signing,
                // Claims on whether he's a sponsor or not, tier, etc.
                claims: principal.Claims
            ));

            return new ContentResult
            {
                Content = jwt,
                ContentType = "application/jwt",
                StatusCode = 200
            };
        }
        else
        {
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
    }
}