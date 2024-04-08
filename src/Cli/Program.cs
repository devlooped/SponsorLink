using Devlooped.Sponsors;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

if (!GitHub.IsInstalled)
{
    AnsiConsole.MarkupLine("[yellow]Please install GitHub CLI from [/][link]https://cli.github.com/[/]");
    return -1;
}

if (!Variables.FirstRunCompleted)
    args = ["welcome"];

if (GitHub.Authenticate() is not { } account)
{
    if (!AnsiConsole.Confirm(ThisAssembly.Strings.GitHub.Login))
    {
        AnsiConsole.MarkupLine("[grey]-[/] Please run [yellow]gh auth login[/] to authenticate, [yellow]gh auth status -h github.com[/] to verify your status.");
        return -1;
    }

    var process = System.Diagnostics.Process.Start("gh", "auth login");

    process.WaitForExit();
    if (process.ExitCode != 0)
        return process.ExitCode;

    account = GitHub.Authenticate();
    if (account is null)
    {
        AnsiConsole.MarkupLine("[red]x[/] Could not retrieve authenticated user with GitHub CLI.");
        AnsiConsole.MarkupLine("[grey]-[/] Please run [yellow]gh auth login[/] to authenticate, [yellow]gh auth status -h github.com[/] to verify your status.");
        return -1;
    }
}
else if (account.Emails.Length == 0)
{
    if (AnsiConsole.Confirm(ThisAssembly.Strings.GitHub.UserScope))
    {
        var process = System.Diagnostics.Process.Start("gh", "auth refresh -h github.com -s user");
        process.WaitForExit();
        if (process.ExitCode != 0)
            return process.ExitCode;

        // Re-authenticate to read emails again.
        account = GitHub.Authenticate();
        if (account is null)
        {
            AnsiConsole.MarkupLine("[red]x[/] Could not retrieve authenticated user with GitHub CLI.");
            AnsiConsole.MarkupLine("[grey]-[/] Please run [yellow]gh auth login[/] to authenticate, [yellow]gh auth status -h github.com[/] to verify your status.");
            return -1;
        }
    }
    else
    {
        AnsiConsole.MarkupLine("[red]x[/] Could not retrieve authenticated user's email(s). This is required to sync your sponsorship manifest.");
        AnsiConsole.MarkupLine("[grey]-[/] Please run [yellow]gh auth refresh -h github.com -s user[/] to enable 'user' scope and fix that manually.");
        return -1;
    }
}

// Provide the authenticated GH CLI user account via DI
var registrations = new ServiceCollection();
registrations.AddSingleton(account);
var registrar = new TypeRegistrar(registrations);

var app = new CommandApp<SyncCommand>(registrar);
registrations.AddSingleton<ICommandApp>(app);

app.Configure(config =>
{
#if GH
    // Change so it matches the actual user experience as a GH CLI extension
    config.SetApplicationName("gh sponsors");
#endif

    config.AddCommand<InitCommand>();
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
                "welcome",
            ]));

    args = [command];
}
#endif

return app.Run(args);
