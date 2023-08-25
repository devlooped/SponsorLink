using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Web.Http;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.ManagementApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.SponsorLink;

public class Functions
{
    static RSA? rsa;
    readonly IConfiguration configuration;

    public Functions(IConfiguration configuration) => this.configuration = configuration;

    [FunctionName("remove")]
    public async Task<IActionResult> RemoveAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req, ILogger logger)
    {
        if (req.Headers.Authorization is null ||
            await Security.ValidateTokenAsync(req.Headers.Authorization) is not ClaimsPrincipal principal ||
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value is not string userId)
            return new UnauthorizedResult();

        var auth = new AuthenticationApiClient(new Uri("https://sponsorlink.us.auth0.com/"));
        if (configuration["Auth0:ClientId"] is not string clientId ||
            configuration["Auth0:ClientSecret"] is not string clientSecret)
        {
            logger.LogError("Missing 'Auth0:ClientId' or 'Auth0:ClientSecret' configuration");
            return new InternalServerErrorResult();
        }

        var token = await auth.GetTokenAsync(new ClientCredentialsTokenRequest
        {
            Audience = "https://sponsorlink.us.auth0.com/api/v2/",
            ClientId = clientId,
            ClientSecret = clientSecret,
        });

        var client = new ManagementApiClient(token.AccessToken, new Uri("https://sponsorlink.us.auth0.com/api/v2"));

        await client.Users.DeleteAsync(userId);

        return new OkResult();
    }

    [FunctionName("sign")]
    public async Task<IActionResult> SignAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req, ILogger logger)
    {
        rsa ??= InitializeKey(configuration, logger);
        if (rsa == null)
            return new InternalServerErrorResult();

        if (req.Headers.Authorization is null ||
            await Security.ValidateTokenAsync(req.Headers.Authorization) is not ClaimsPrincipal principal ||
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Split('|')?[1] is not string id)
            return new UnauthorizedResult();

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            ValidAudience = "SponsorLink",
            ValidIssuer = "Devlooped",
            ValidateIssuerSigningKey = false,
        };

        if (req.Content == null)
            return new BadRequestResult();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(await req.Content.ReadAsStringAsync());
        if (jwt.Issuer != "Devlooped" || jwt.Audiences.FirstOrDefault() != "SponsorLink")
            return new BadRequestResult();

        // "sub" claim must match between token claims and principal
        if (jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value != id)
            return new BadRequestResult();

        var signing = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        // Expire the first day of the next month
        var expiration = DateTime.UtcNow.AddMonths(1);
        expiration = new DateTime(expiration.Year, expiration.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var signed = new JwtSecurityToken(
            issuer: "Devlooped",
            audience: "SponsorLink",
            claims: jwt.Claims.Where(c => c.Type != "exp" && c.Type != "aud" && c.Type != "iss"),
            expires: expiration,
            signingCredentials: signing);

        // Serialize the token and return as a string
        var body = new JwtSecurityTokenHandler().WriteToken(signed);

        return new ContentResult
        {
            Content = body,
            ContentType = "text/plain",
            StatusCode = (int)HttpStatusCode.OK,
        };
    }

    static RSA? InitializeKey(IConfiguration configuration, ILogger logger)
    {
        if (configuration["SPONSORLINK_KEY"] is not string key)
        {
            logger.LogError("Missing SPONSORLINK_KEY configuration");
            return null;
        }

        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(key), out _);

        return rsa;
    }
}
