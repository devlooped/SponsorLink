using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Devlooped.SponsorLink;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, builder) =>
    {
        builder.AddUserSecrets("A85AC898-E41C-4D9D-AD9B-52ED748D9901");
    })
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
        services.AddHttpClient("github", http =>
        {
            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink", ThisAssembly.Info.InformationalVersion));
        });
        services.AddHttpClient("sponsorable", (sp, http) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["GH_TOKEN"] is not { Length: > 0 } ghtoken)
            {
                sp.GetRequiredService<ILogger<Sync>>().LogWarning("Missing required configuration GH_TOKEN");
                throw new InvalidOperationException("Missing required configuration GH_TOKEN");
            }

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink", ThisAssembly.Info.InformationalVersion));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
        });
        services.AddMemoryCache();
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["SPONSORLINK_KEY"] is not { Length: > 0 } key)
            {
                sp.GetRequiredService<ILogger<Sync>>().LogError("Missing required configration SPONSORLINK_KEY");
                throw new InvalidOperationException("Missing required configuration SPONSORLINK_KEY");
            }

            // The key (as well as the yaml manifest) can be generated using gh sponsors init
            // Install with: gh extension install devlooped/gh-sponsors
            // See: https://github.com/devlooped/gh-sponsors
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(key), out _);

            return rsa;
        });
    })
    .Build();

host.Run();
