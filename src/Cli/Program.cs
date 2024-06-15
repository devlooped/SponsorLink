﻿#pragma warning disable CS0436 // Type conflicts with imported type
using System.Diagnostics;
using Devlooped.Sponsors;
using DotNetConfig;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

#if DEBUG
if (args.Contains("--debug"))
{
    Debugger.Launch();
    args = args.Where(x => x != "--debug").ToArray();
}
#endif

var app = App.Create(out var services);

if (args.Contains("-?"))
    args = args.Select(x => x == "-?" ? "-h" : x).ToArray();

app.Configure(config => config.SetApplicationName(ThisAssembly.Project.ToolCommandName));

if (args.Contains("--version"))
{
    AnsiConsole.MarkupLine($"{ThisAssembly.Project.ToolCommandName} version [lime]{ThisAssembly.Project.Version}[/] ({ThisAssembly.Project.BuildDate})");
    AnsiConsole.MarkupLine($"[link]{ThisAssembly.Git.Url}/releases/tag/{ThisAssembly.Project.BuildRef}[/]");
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

return app.Run(args);
