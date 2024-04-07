using System.Net.Http.Headers;
using System.Security.Cryptography;
using Azure.Identity;
using Devlooped.Sponsors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, builder) =>
    {
        builder.AddUserSecrets("A85AC898-E41C-4D9D-AD9B-52ED748D9901");
        if (context.Configuration["Azure:KeyVault"] is string kv)
            builder.AddAzureKeyVault(new Uri($"https://{kv}.vault.azure.net/"), new DefaultAzureCredential());
    })
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseFunctionContextAccessor();
        builder.UseErrorLogging();
        builder.UseAppServiceAuthentication();
        builder.UseGitHubAuthentication();
        builder.UseClaimsPrincipal();
    })
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddMemoryCache();

        // Add sponsorable client using the GH_TOKEN for GitHub API access
        services.AddHttpClient("sponsorable", (sp, http) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["GH_TOKEN"] is not { Length: > 0 } ghtoken)
            {
                sp.GetRequiredService<ILogger<Sync>>().LogWarning("Missing required configuration GH_TOKEN");
                throw new InvalidOperationException("Missing required configuration GH_TOKEN");
            }

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
        });

        // Add sponsor client using the current invocation claims for GitHub API access
        services.AddScoped<AccessTokenMessageHandler>();
        services.AddHttpClient("sponsor", http =>
        {
            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
        }).AddHttpMessageHandler<AccessTokenMessageHandler>();

        // RSA key for JWT signing
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["SponsorLink:Private"] is not { Length: > 0 } key)
            {
                sp.GetRequiredService<ILogger<Sync>>().LogError("Missing required configuration 'SponsorLink:Private'.");
                throw new InvalidOperationException("Missing required configuration 'SponsorLink:Private'.");
            }

            // The key (as well as the yaml manifest) can be generated using gh sponsors init
            // Install with: gh extension install devlooped/gh-sponsors
            // See: https://github.com/devlooped/gh-sponsors
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(key), out _);

            return rsa;
        });

        services.AddSingleton<SponsorsManager>();
    })
.Build();

host.Run();
