using System.Diagnostics;
using System.Net.Http.Headers;
using Devlooped.Sponsors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Devlooped;

class Helpers
{
    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddUserSecrets<Helpers>()
        .AddEnvironmentVariables()
        .AddInMemoryCollection()
        .Build();

    public static IServiceProvider Services { get; }

    static Helpers()
    {
        var collection = new ServiceCollection()
            .AddHttpClient().ConfigureHttpClientDefaults(defaults => defaults.ConfigureHttpClient(http =>
            {
                http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
                if (Debugger.IsAttached)
                    http.Timeout = TimeSpan.FromMinutes(10);
            }))
            .AddLogging()
            .AddSingleton<IConfiguration>(Configuration)
            .AddAsyncLazy();

        collection.AddGraphQueryClient();

        collection
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

        collection.AddScoped(services => services.GetRequiredService<IOptions<SponsorLinkOptions>>().Value);

        collection.AddOptions<PushoverOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("Pushover").Bind(options);
            });

        collection.AddScoped(services => services.GetRequiredService<IOptions<PushoverOptions>>().Value);

        if (Configuration["GitHub:Token"] is { Length: > 0 } ghtoken)
        {
            collection.AddHttpClient("GitHub", http =>
            {
                http.BaseAddress = new Uri("https://api.github.com");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
            });

            collection.AddHttpClient("GitHub:Token", http =>
            {
                http.BaseAddress = new Uri("https://api.github.com");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
            });
        }

        if (Configuration["GitHub:Sponsorable"] is { Length: > 0 } ghsponsorable)
        {
            collection.AddHttpClient("GitHub:Sponsorable", http =>
            {
                http.BaseAddress = new Uri("https://api.github.com");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghsponsorable);
            });
            collection.AddHttpClient("sponsorable", http =>
            {
                http.BaseAddress = new Uri("https://api.github.com");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghsponsorable);
            });
        }

        Services = collection.BuildServiceProvider();
    }
}
