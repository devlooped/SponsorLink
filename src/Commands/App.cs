using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

public static class App
{
    public static CommandApp Create()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGraphQueryClient>(new CliGraphQueryClient());
        services.AddSingleton<IGitHubAppAuthenticator>(sp => new GitHubAppAuthenticator(sp.GetRequiredService<IHttpClientFactory>()));
        services.AddHttpClient().ConfigureHttpClientDefaults(defaults => defaults.ConfigureHttpClient(http => 
        {
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
            if (Debugger.IsAttached)
                http.Timeout = TimeSpan.FromMinutes(10);
        }));
        services.AddHttpClient("GitHub", http =>
        {
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = false,
        });

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        registrar.Services.AddSingleton<ICommandApp>(app);

        app.Configure(config =>
        {
            config.AddCommand<InitCommand>();
            config.AddCommand<ListCommand>();
            config.AddCommand<SyncCommand>();
            config.AddCommand<ValidateCommand>();
            config.AddCommand<WelcomeCommand>();
        });

        return app;
    }
}
