using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Devlooped;
using Devlooped.Sponsors;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.AzureFunctions;

var host = new HostBuilder()
    .ConfigureAppConfiguration(builder => builder.Configure())
    .ConfigureFunctionsWebApplication(builder =>
    {
        builder.UseFunctionContextAccessor();
        builder.UseErrorLogging();
#if DEBUG
        builder.UseGitHubDeviceFlowAuthentication();
#endif
        builder.UseAppServiceAuthentication();
        builder.UseGitHubAuthentication(populateEmails: true, verifiedOnly: true);
        builder.UseClaimsPrincipal();
        builder.UseActivityTelemetry();
    })
    .ConfigureServices(services =>
    {
        // Register first so it initializes always before every other initializer.
        services.AddSingleton<ITelemetryInitializer, ApplicationVersionTelemetryInitializer>();
        services.AddSingleton(sp => CloudStorageAccount.Parse(sp.GetRequiredService<IConfiguration>()["AzureWebJobsStorage"] ??
            throw new InvalidOperationException("Missing required configuration 'AzureWebJobsStorage'.")));

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddActivityTelemetry();

        services.AddMemoryCache();
        services.AddOptions();
        services.AddSingleton<Lazy<TelemetryClient>, Lazy<TelemetryClient>>(sp => new(() => sp.GetRequiredService<TelemetryClient>()));

        JsonWebTokenHandler.DefaultMapInboundClaims = false;

        services
            .AddOptions<SponsorLinkOptions>()
            .Configure<IConfiguration, IGraphQueryClientFactory>((options, configuration, client) =>
            {
                configuration.GetSection("SponsorLink").Bind(options);
                // Ensure default value is populated from the logged in account if we can't find it in the configuration.
                if (string.IsNullOrEmpty(options.Account))
                    options.Account = client.CreateClient("sponsorable").QueryAsync(GraphQueries.ViewerAccount).GetAwaiter().GetResult()?.Login;
            })
            .ValidateOnStart()
            .ValidateDataAnnotations();

        services.AddScoped(services => services.GetRequiredService<IOptions<SponsorLinkOptions>>().Value);

        services.AddOptions<PushoverOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("Pushover").Bind(options);
            });

        services.AddScoped(services => services.GetRequiredService<IOptions<PushoverOptions>>().Value);

        services.AddHttpClient().ConfigureHttpClientDefaults(defaults => defaults.ConfigureHttpClient(http =>
        {
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
        }));

        // Add sponsorable client using the GH_TOKEN for GitHub API access
        services.AddHttpClient("sponsorable", (sp, http) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["GitHub:Token"] is not { Length: > 0 } ghtoken)
                throw new InvalidOperationException("Missing required configuration 'GitHub:Token'");

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
        });

        // Add sponsor client using the current invocation claims for GitHub API access
        services.AddScoped<AccessTokenMessageHandler>();
        services.AddHttpClient("sponsor", http =>
        {
            http.BaseAddress = new Uri("https://api.github.com");
        }).AddHttpMessageHandler<AccessTokenMessageHandler>();

        services.AddGraphQueryClient();

        // RSA key for JWT signing
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SponsorLinkOptions>>();
            if (string.IsNullOrEmpty(options.Value.PrivateKey))
                throw new InvalidOperationException($"Missing required configuration 'SponsorLink:{nameof(SponsorLinkOptions.PrivateKey)}'");

            // The key (as well as the yaml manifest) can be generated using sponsors init
            // Install with: gh extension install devlooped/gh-sponsors
            // See: https://github.com/devlooped/gh-sponsors
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(options.Value.PrivateKey), out _);

            return rsa;
        });

        services.AddSingleton<IGitHubClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new GitHubClient(new Octokit.ProductHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion))
            {
                Credentials = new Credentials(config["SponsorLink:BotToken"] ?? config["GitHub:BotToken"] ?? config["GitHub:Token"] ??
                    throw new InvalidOperationException("Could not determine token to use for GitHub issues/pull request updates."))
            };
        });

        services.AddSingleton<SponsorsManager>();
        services.AddSingleton<SponsoredIssues>();
        services.AddSingleton<WebhookEventProcessor, Webhook>();
        services.AddSingleton<IPushover, Pushover>();

        services.AddSingleton(sp => new AsyncLazy<OpenSource>(async () =>
        {
            using var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var response = await http.GetAsync("https://raw.githubusercontent.com/devlooped/nuget/refs/heads/main/nuget.json");

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to fetch open source data: {response.StatusCode}");

            var oss = await JsonSerializer.DeserializeAsync<OpenSource>(response.Content.ReadAsStream(), JsonOptions.Default)
                ?? throw new InvalidOperationException("Failed to deserialize open source data.");

            return oss;
        }));

    })
    .ConfigureGitHubWebhooks(config => config["GitHub:Secret"] ?? throw new ArgumentException("Missing GitHub:Secret configuration"))
    .Build();

host.Run();
