using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using DotNetConfig;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

public static class App
{
    public static CommandApp Create(out IServiceProvider services)
    {
        var collection = new ServiceCollection();

        collection.AddSingleton(sp =>
        {
            var sldir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink");
            Directory.CreateDirectory(sldir);
            return Config.Build(sldir);
        });

        collection.AddSingleton<IGraphQueryClient>(new CliGraphQueryClient());
        collection.AddSingleton<IGitHubAppAuthenticator>(sp => new GitHubAppAuthenticator(sp.GetRequiredService<IHttpClientFactory>()));
        collection.AddHttpClient().ConfigureHttpClientDefaults(defaults => defaults.ConfigureHttpClient(http =>
        {
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
            if (Debugger.IsAttached)
                http.Timeout = TimeSpan.FromMinutes(10);
        }));
        collection.AddHttpClient("GitHub", http =>
        {
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = false,
        });

        var registrar = new TypeRegistrar(collection);
        var app = new CommandApp(registrar);
        registrar.Services.AddSingleton<ICommandApp>(app);

        app.Configure(config =>
        {
            config.AddCommand<ConfigCommand>();
            config.AddCommand<InitCommand>();
            config.AddCommand<ListCommand>();
            config.AddCommand<SyncCommand>();
            config.AddCommand<ViewCommand>();
            config.AddCommand<WelcomeCommand>();
        });

        services = registrar.Services.BuildServiceProvider();

        return app;
    }
}
