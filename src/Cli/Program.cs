#pragma warning disable CS0436 // Type conflicts with imported type
using System.Diagnostics;
using Devlooped.Sponsors;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

#if DEBUG
if (args.Contains("--debug"))
{
    Debugger.Launch();
    args = args.Where(x => x != "--debug").ToArray();
}
#endif

var app = App.Create();

if (args.Contains("-?"))
    args = args.Select(x => x == "-?" ? "-h" : x).ToArray();

app.Configure(config => config.SetApplicationName("sl"));

#if GH
app.Configure(config =>
{
    // Change so it matches the actual user experience as a GH CLI extension
    config.SetApplicationName("gh sponsors");
});
#endif

if (!Variables.FirstRunCompleted || args.Contains("--welcome"))
{
    app.SetDefaultCommand<WelcomeCommand>();
    return await app.RunAsync([]);
}

#if DEBUG
if (args.Length == 0)
{
    var command = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Command to run:")
            .AddChoices(
            [
                "init",
                "list",
                "sync",
                "welcome",
            ]));

    args = [command];
}
#endif

return app.Run(args);
