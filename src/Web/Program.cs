using System.Security.Claims;
using Devlooped.SponsorLink;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseMiddleware<ErrorLoggingMiddleware>();
        builder.UseMiddleware<ClientPrincipalMiddleware>();
        builder.UseMiddleware<GitHubTokenMiddleware>();
    })
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpClient();
    })
    .Build();

host.Run();
