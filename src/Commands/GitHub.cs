using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Spectre.Console;
using static Devlooped.Sponsors.Process;

namespace Devlooped.Sponsors;

public record AccountInfo(int Id, string Login)
{
    public string[] Emails { get; init; } = Array.Empty<string>();
}

public static class GitHub
{
    public static bool IsInstalled { get; } = TryIsInstalled(out var _);

    public static bool TryIsInstalled(out string? output)
        => TryExecute("gh", "--version", out output) && output?.StartsWith("gh version") == true;

    public static bool TryApi(string endpoint, string jq, out string? json)
    {
        var args = $"api {endpoint}";
        if (jq?.Length > 0)
            args += $" --jq \"{jq}\"";

        return TryExecute("gh", args, out json);
    }

    public static bool TryAuthenticate([NotNullWhen(true)] out AccountInfo? account)
    {
        account = null;

        if (!IsInstalled)
        {
            AnsiConsole.MarkupLine("[yellow]Please install GitHub CLI from [/][link]https://cli.github.com/[/]");
            return false;
        }

        account = Authenticate();
        if (account is not null)
            return true;

        // Continuing from here requires an interactive console.
        // See https://stackoverflow.com/questions/1188658/how-can-a-c-sharp-windows-console-application-tell-if-it-is-run-interactively
        if (!Environment.UserInteractive || Console.IsInputRedirected || Console.IsOutputRedirected || Console.IsErrorRedirected)
            return false;

        if (!AnsiConsole.Confirm(ThisAssembly.Strings.GitHub.Login))
        {
            AnsiConsole.MarkupLine("[dim]-[/] Please run [yellow]gh auth login[/] to authenticate, [yellow]gh auth status -h github.com[/] to verify your status.");
            return false;
        }

        var process = System.Diagnostics.Process.Start("gh", "auth login");

        process.WaitForExit();
        if (process.ExitCode != 0)
            return false;

        account = Authenticate();
        if (account is not null)
            return true;

        AnsiConsole.MarkupLine("[red]x[/] Could not retrieve authenticated user with GitHub CLI.");
        AnsiConsole.MarkupLine("[dim]-[/] Please run [yellow]gh auth login[/] to authenticate, [yellow]gh auth status -h github.com[/] to verify your status.");
        return false;
    }

    public static IDisposable? WithToken(string? token)
    {
        if (token == null)
            return null;

        if (!IsInstalled)
        {
            AnsiConsole.MarkupLine("[yellow]Please install GitHub CLI from [/][link]https://cli.github.com/[/]");
            return null;
        }

        return TransientToken.TrySet(token);
    }

    public static AccountInfo? Authenticate()
    {
        if (!TryExecute("gh", "auth status -h github.com", out var output) || output is null)
            return default;

        if (output.Contains("gh auth login"))
            return default;

        if (!TryExecute("gh", "api user", out output) || output is null)
            return default;

        if (JsonSerializer.Deserialize<AccountInfo>(output, JsonOptions.Default) is not { } account)
            return default;

        if (!TryApi("user/emails", "[.[] | select(.verified == true) | .email]", out output) ||
            string.IsNullOrEmpty(output))
            return account;

        return account with
        {
            Emails = JsonSerializer.Deserialize<string[]>(output, JsonOptions.Default) ?? []
        };
    }

    class TransientToken : IDisposable
    {
        readonly string? existingToken;

        public static TransientToken? TrySet(string token)
        {
            var transient = new TransientToken(token);

            if (!TryExecute("gh", $"auth login --with-token", token, out var output))
            {
                Debug.Fail(output);
                return null;
            }

            return transient;
        }

        TransientToken(string token)
        {
            if (TryExecute("gh", "auth status", out var _))
                TryExecute("gh", "auth token", out existingToken);
        }

        public void Dispose()
        {
            if (existingToken != null &&
                TryExecute("gh", "auth token", out var currentToken) &&
                existingToken != currentToken)
            {
                string? output = default;
                Debug.Assert(TryExecute("gh", $"auth login --with-token", existingToken, out output), output);
                Debug.Assert(TryExecute("gh", "auth status", out output), output);
                Debug.Assert(TryExecute("gh", "auth token", out var newToken), newToken);
                Debug.Assert(newToken == existingToken, "Could not restore previous auth token.");
            }
        }
    }
}
