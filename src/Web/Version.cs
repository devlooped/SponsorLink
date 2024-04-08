using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Devlooped.Sponsors;

class Version(IConfiguration configuration)
{
    [Function("version")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        => new ContentResult
        {
            Content = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3),
            ContentType = "text/plain",
            StatusCode = 200,
        };

    [Function("test")]
    public IActionResult Test([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        => new ContentResult
        {
            Content = configuration["Azure:KeyVault"],
            ContentType = "text/plain",
            StatusCode = 200,
        };

}
