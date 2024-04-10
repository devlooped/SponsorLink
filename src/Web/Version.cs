using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

class Version(IConfiguration configuration, ILogger<Version> logger, IWebHostEnvironment hosting)
{
    [Function("version")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
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

    [Function("pub")]
    public IActionResult Test([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        if (configuration["SponsorLink:Public"] is not { Length: > 0 } key)
        {
            logger.LogError("Missing required configuration 'SponsorLink:Public'");
            return new StatusCodeResult(500);
        }

        if (req.GetTypedHeaders().Accept?.Any(x =>
                x.MediaType == "application/json" ||
                x.MediaType == "application/jwk+json") == true)
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(key), out _);
            return new ContentResult
            {
                Content = JsonSerializer.Serialize(
                    JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false))),
                    JsonOptions.JsonWebKey),
                ContentType = "application/jwk+json",
                StatusCode = 200,
            };
        }

        return new ContentResult
        {
            Content = key,
            ContentType = "text/plain",
            StatusCode = 200,
        };
    }
}
