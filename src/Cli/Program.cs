using Devlooped.Sponsors;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

// Provide the authenticated GH CLI user account via DI
var registrations = new ServiceCollection();
registrations.AddSingleton<IGraphQueryClient>(new CliGraphQueryClient());
var registrar = new TypeRegistrar(registrations);

if (!Variables.FirstRunCompleted)
{
    if (await new CommandApp<WelcomeCommand>(registrar).RunAsync([]) != 0)
        return -1;
}

var app = new CommandApp(registrar);
registrations.AddSingleton<ICommandApp>(app);

app.Configure(config =>
{
#if GH
    // Change so it matches the actual user experience as a GH CLI extension
    config.SetApplicationName("gh sponsors");
#endif

    config.AddCommand<InitCommand>();
    config.AddCommand<ListCommand>();
    config.AddCommand<WelcomeCommand>();

#if DEBUG
    config.ValidateExamples();
#endif
});

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
                "welcome",
            ]));

    args = [command];
}
#endif

return app.Run(args);
