using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Devlooped.SponsorLink;

/// <summary>
/// Returns a JWT or JSON manifest of the authenticated user's claims.
/// </summary>
public class Sync(IConfiguration configuration)
{
    /// <summary>
    /// Depending on the Accept header, returns a JWT or JSON manifest of the authenticated user's claims.
    /// </summary>
    [Function("sync")]
    public IActionResult RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, FunctionContext context)
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

        // We always respond authenticated requests either with a JWT or JSON, depending on the Accept header.
        if (req.Headers.Accept.Contains("application/jwt"))
        {
            var token = new JwtSecurityToken(
                // TODO?
                // audience: audience,
                // issuer: issuer,
                // signingCredentials: private key...
                claims: principal.Claims
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            
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
                claims = principal.Claims.ToDictionary(x => x.Type, x => x.Value),
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