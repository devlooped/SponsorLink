using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Devlooped.Sponsors;
using DotNetConfig;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Cli;

// Some users reported not getting emoji on Windows, so we force UTF-8 encoding.
// This not great, but I couldn't find a better way to do it.
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    Console.InputEncoding = Console.OutputEncoding = Encoding.UTF8;

#if DEBUG
if (args.Contains("--debug"))
{
    Debugger.Launch();
    args = args.Where(x => x != "--debug").ToArray();
}
#endif

var app = App.Create(out var services);
#if DEBUG
app.Configure(c => c.PropagateExceptions());
#else
if (args.Contains("--exceptions"))
{
    app.Configure(c => c.PropagateExceptions());
    args = args.Where(x => x != "--exceptions").ToArray();
}
#endif

if (args.Contains("-?"))
    args = args.Select(x => x == "-?" ? "-h" : x).ToArray();

app.Configure(config => config.SetApplicationName(ThisAssembly.Project.ToolCommandName));

if (args.Contains("--version"))
{
    AnsiConsole.MarkupLine($"{ThisAssembly.Project.ToolCommandName} version [lime]{ThisAssembly.Project.Version}[/] ({ThisAssembly.Project.BuildDate})");
    AnsiConsole.MarkupLine($"[link]{ThisAssembly.Git.Url}/releases/tag/{ThisAssembly.Project.BuildRef}[/]");

    foreach (var message in await CheckUpdates(args))
        AnsiConsole.MarkupLine(message);

    return 0;
}

// If we don't have ToS acceptance, we don't run any command other than welcome.
var tos = services.GetRequiredService<Config>().TryGetBoolean("sponsorlink", "tos", out var completed) && completed;
if (!tos && args.Contains(ToSSettings.ToSOption))
{
    // Implicit acceptance on first run of another tool, like `sync --tos`
    app.Run(["config", ToSSettings.ToSOption, "--quiet"]);
}
else if (!tos || args.Contains("--welcome"))
{
    // Force run welcome if --welcome is passed or no tos was accepted yet
    // preserve all other args just in case the welcome command adds more in the future.
    var result = await app.RunAsync(args.TakeWhile(x => x == ToSSettings.ToSOption).Prepend("welcome").ToArray());

    if (result != 0)
        return result;

    args = args.SkipWhile((s, i) => i == 0 && s == "welcome").Where(x => x != "--welcome").ToArray();
}

#if DEBUG
if (args.Length == 0)
{
    var command = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Command to run:")
            .AddChoices(
            [
                "config",
                "init",
                "list",
                "sync",
                "validate",
                "welcome",
            ]));

    args = [command];
}
#endif

var updates = Task.Run(() => CheckUpdates(args));
var exit = app.Run(args);

if (await updates is { Length: > 0 } messages)
{
    foreach (var message in messages)
        AnsiConsole.MarkupLine(message);
}

return exit;

static async Task<string[]> CheckUpdates(string[] args)
{
    if (args.Contains("-u") && !args.Contains("--unattended"))
        return [];

    var providers = Repository.Provider.GetCoreV3();
    var repository = new SourceRepository(new PackageSource("https://api.nuget.org/v3/index.json"), providers);
    var resource = await repository.GetResourceAsync<PackageMetadataResource>();
    var localVersion = new NuGetVersion(ThisAssembly.Project.Version);
    var metadata = await resource.GetMetadataAsync(ThisAssembly.Project.PackageId, true, false,
        new SourceCacheContext
        {
            NoCache = true,
            RefreshMemoryCache = true,
        },
        NuGet.Common.NullLogger.Instance, CancellationToken.None);

    var update = metadata
        .Select(x => x.Identity)
        .Where(x => x.Version > localVersion)
        .OrderByDescending(x => x.Version)
        .Select(x => x.Version)
        .FirstOrDefault();

    if (update != null)
    {
        return [
            $"There is a new version of [yellow]{ThisAssembly.Project.PackageId}[/]: [dim]v{localVersion.ToNormalizedString()}[/] -> [lime]v{update.ToNormalizedString()}[/]",
            $"Update with: [yellow]dotnet[/] tool update -g {ThisAssembly.Project.PackageId}"
        ];
    }

    return [];
}