using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

class Version(IConfiguration configuration, SponsorsManager sponsors, RSA rsa, ILogger<Version> logger, IOptions<SponsorLinkOptions> options, IWebHostEnvironment hosting)
{
    SponsorLinkOptions options = options.Value;

    [Function("status")]
    public async Task<IActionResult> GetStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var manifest = await sponsors.GetManifestAsync();
        var jwt = new JwtSecurityTokenHandler { MapInboundClaims = false }.ReadJwtToken(await sponsors.GetRawManifestAsync());

        var json = jwt.Payload.SerializeToJson();
        var doc = JsonDocument.Parse(json);

        if (!rsa.ThumbprintEquals(manifest.SecurityKey))
        {
            logger.LogError($"Configured private key 'SponsorLink:{nameof(SponsorLinkOptions.PrivateKey)}' does not match the manifest public key.");
            return new StatusCodeResult(500);
        }

        return new ContentResult()
        {
            Content = JsonSerializer.Serialize(doc, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            ContentType = "text/plain",
            StatusCode = 200,
        };
    }

    [Function("version")]
    public IActionResult GetVersion([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (hosting.EnvironmentName == "Development" || configuration["DEBUG"] == "true")
        {
            foreach (var item in configuration.AsEnumerable())
            {
                logger.LogInformation("{Key} = {Value}", item.Key, item.Value);
            }
        }

        return new ContentResult
        {
            Content = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3),
            ContentType = "text/plain",
            StatusCode = 200,
        };
    }
}
