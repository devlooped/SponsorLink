using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

class Version(IConfiguration configuration, ILogger<Version> logger, IWebHostEnvironment hosting)
{
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
            Content = ThisAssembly.Info.InformationalVersion,
            ContentType = "text/plain",
            StatusCode = 200,
        };
    }
}
