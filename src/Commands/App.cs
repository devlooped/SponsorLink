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
        var sldir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink");
        Directory.CreateDirectory(sldir);

        var config = Config.Build(sldir);
        // Auto-initialize an opaque instance/installation id
        if (!config.TryGetString("sponsorlink", "id", out var id))
        {
            id = Guid.NewGuid().ToString();
            config = config.SetString("sponsorlink", "id", id);
        }

        // Don't propagate traceparent from client (we don't collect telemetry to correlate).
        DistributedContextPropagator.Current = DistributedContextPropagator.CreateNoOutputPropagator();
        ActivitySource.AddActivityListener(new ActivityListener
        {
            // Forces our activities to be created, thereby being available to pull into headers
            ShouldListenTo = activity => activity.Name.StartsWith("Devlooped"),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.PropagationData,
        });

        // Made transient so each command gets a new copy with potentially updated values.
        collection.AddTransient(sp => Config.Build(sldir));
        collection.AddSingleton<IGraphQueryClient>(new CliGraphQueryClient());
        collection.AddSingleton<IGitHubAppAuthenticator>(sp => new GitHubAppAuthenticator(sp.GetRequiredService<IHttpClientFactory>()));
        collection.AddHttpClient().ConfigureHttpClientDefaults(defaults => defaults.ConfigureHttpClient(http =>
        {
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
            if (Debugger.IsAttached)
                http.Timeout = TimeSpan.FromMinutes(10);

            var optout = Environment.GetEnvironmentVariable("SPONSOR_CLI_TELEMETRY_OPTOUT");
            if (optout == null || (optout != "1" && optout != "true"))
                http.DefaultRequestHeaders.TryAddWithoutValidation("x-telemetry-id", id);

            if (Activity.Current is { } activity)
                http.DefaultRequestHeaders.TryAddWithoutValidation("x-telemetry-operation", activity.OperationName);
        }));
        collection.AddHttpClient("GitHub", http =>
        {
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = false,
        });

        collection.AddTransient<ICommandInterceptor, ActivityCommandInterceptor>();

        var registrar = new TypeRegistrar(collection);
        var app = new CommandApp(registrar);
        registrar.Services.AddSingleton<ICommandApp>(app);

        app.Configure(config =>
        {
            config.AddCommand<ConfigCommand>();
            config.AddCommand<InitCommand>();
            config.AddCommand<ListCommand>();
            config.AddCommand<RemoveCommand>();
            config.AddCommand<SyncCommand>();
            config.AddCommand<ViewCommand>();
            config.AddCommand<WelcomeCommand>().IsHidden();
            config.AddCommand<NuGetStatsCommand>("nuget")
#if !DEBUG
                .IsHidden()
#endif
                ;
#if DEBUG
            config.AddCommand<CheckTokenCommand>("check");
#endif
        });

        services = registrar.Services.BuildServiceProvider();

        return app;
    }

    class ActivityCommandInterceptor : ICommandInterceptor
    {
        Activity? activity;

        public void Intercept(CommandContext context, CommandSettings settings)
            => activity = ActivityTracer.Source.StartActivity(context.Name, ActivityKind.Client);

        public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
        {
            activity?.SetTag("ExitCode", result);
            activity?.Dispose();
        }
    }
}
