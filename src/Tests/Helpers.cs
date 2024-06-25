using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

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
            .AddSingleton(_ => AsyncLazy.Create(async () =>
            {
                var playwright = await Playwright.CreateAsync();
                var options = new BrowserTypeLaunchOptions
                {
                    Headless = !Debugger.IsAttached,
                };

                if (OperatingSystem.IsWindows())
                    options.Channel = "msedge";
                else
                    options.ExecutablePath = Chromium.Path;

                return await playwright.Chromium.LaunchAsync(options);
            }))
            .AddAsyncLazy();

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
        }

        Services = collection.BuildServiceProvider();
    }
}
