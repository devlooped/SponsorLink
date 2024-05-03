using Spectre.Console.Cli;
using static Devlooped.Sponsors.GitHubCommand;

namespace Devlooped.Sponsors;

/// <summary>
/// Ensures proper GH CLI authentication.
/// </summary>
/// <remarks>
/// Ensures the user is signed in the GH CLI and has run the 
/// first-run experience and accepted the overall license.
/// Returns -1 if the user isn't signed in. -2 if usage terms were 
/// not accepted (WelcomeCommand result).
/// </remarks>
public abstract class GitHubCommand(ICommandApp app) : Command
{
    /// <summary>
    /// Ensures the user is signed in the GH CLI and has run the 
    /// first-run experience and accepted the overall license.
    /// </summary>
    /// <returns>
    /// -1 if the user isn't signed in. -2 if usage terms were 
    /// not accepted.
    /// </returns>
    internal static int Execute(ICommandApp app, Action<AccountInfo?> callback)
    {
        if (!GitHub.TryAuthenticate(out var account))
            return -1;

        if (!SponsorLink.Variables.FirstRunCompleted &&
            app.Run(["welcome"]) is var result &&
            result < 0)
        {
            return result;
        }

        callback(account);
        return 0;
    }

    public override int Execute(CommandContext context) => Execute(app, acc => Account = acc);

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}

public abstract class GitHubCommand<TSettings>(ICommandApp app) : Command<TSettings> where TSettings : CommandSettings
{
    public override int Execute(CommandContext context, TSettings settings) => GitHubCommand.Execute(app, acc => Account = acc);

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}

public abstract class GitHubAsyncCommand(ICommandApp app) : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context) => Task.FromResult(Execute(app, acc => Account = acc));

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}

public abstract class GitHubAsyncCommand<TSettings>(ICommandApp app) : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    public override Task<int> ExecuteAsync(CommandContext context, TSettings settings) => Task.FromResult(Execute(app, acc => Account = acc));

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}



