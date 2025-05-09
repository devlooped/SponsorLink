﻿using DotNetConfig;
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
public abstract class GitHubCommand(Config config) : Command
{
    /// <summary>
    /// Ensures the user is signed in the GH CLI and has run the 
    /// first-run experience and accepted the overall license.
    /// </summary>
    /// <returns>
    /// -1 if the user isn't signed in. -2 if usage terms were 
    /// not accepted.
    /// </returns>
    internal static int Execute(Config config, Action<AccountInfo?> callback)
    {
        if (!GitHub.TryAuthenticate(out var account))
            return -1;

        // ToS acceptance is already checked and run from the main program.
        callback(account);
        return 0;
    }

    public override int Execute(CommandContext context) => Execute(config, acc => Account = acc);

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}

public abstract class GitHubCommand<TSettings>(Config config) : Command<TSettings> where TSettings : CommandSettings
{
    public override int Execute(CommandContext context, TSettings settings) => GitHubCommand.Execute(config, acc => Account = acc);

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}

public abstract class GitHubAsyncCommand(Config config) : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context) => Task.FromResult(Execute(config, acc => Account = acc));

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}

public abstract class GitHubAsyncCommand<TSettings>(Config config) : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    public override Task<int> ExecuteAsync(CommandContext context, TSettings settings) => Task.FromResult(Execute(config, acc => Account = acc));

    /// <summary>
    /// Authenticated user account in the GH CLI.
    /// </summary>
    protected AccountInfo? Account { get; private set; }
}



