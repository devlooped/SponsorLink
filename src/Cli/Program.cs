#pragma warning disable CS0436 // Type conflicts with imported type
using System.Diagnostics;
using Devlooped.Sponsors;
using Spectre.Console;
using static Devlooped.SponsorLink;

#if DEBUG
if (args.Contains("--debug"))
{
    Debugger.Launch();
    args = args.Where(x => x != "--debug").ToArray();
}
#endif

var app = App.Create();

#if GH
app.Configure(config =>
{
    // Change so it matches the actual user experience as a GH CLI extension
    config.SetApplicationName("gh sponsors");
});
#endif

if (!Variables.FirstRunCompleted)
{
    app.SetDefaultCommand<WelcomeCommand>();
    if (await app.RunAsync([]) != 0)
        return -1;
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
