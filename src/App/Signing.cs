using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.SponsorLink;

public static class Signing
{
    static RSA? rsa;

    [FunctionName("sign")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sign")] HttpRequestMessage req, IConfiguration configuration, ILogger logger)
    {
        rsa ??= InitializeKey(configuration, logger);
        if (rsa == null)
            return new InternalServerErrorResult();

        if (req.Headers.Authorization is null ||
            await Auth0.ValidateTokenAsync(req.Headers.Authorization) is not ClaimsPrincipal principal ||
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

        var signed = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audiences.First(),
            claims: jwt.Claims,
            // Expire at the end of the month
            expires: new DateTime(DateTime.Today.Year, DateTime.Today.Month + 1, 1, 0, 0, 0, DateTimeKind.Utc),
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
