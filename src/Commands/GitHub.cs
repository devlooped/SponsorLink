using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Spectre.Console;

namespace Devlooped.Sponsors;

public record AccountInfo(int Id, string Login)
{
    public string[] Emails { get; init; } = Array.Empty<string>();
}

public static class GitHub
{
    public static bool IsInstalled { get; } = TryIsInstalled(out var _);

    public static bool TryIsInstalled(out string? output)
        => Process.TryExecute("gh", "--version", out output) && output?.StartsWith("gh version") == true;

    public static bool TryApi(string endpoint, string jq, out string? json)
    {
        var args = $"api {endpoint}";
        if (jq?.Length > 0)
            args += $" --jq \"{jq}\"";

        return Process.TryExecute("gh", args, out json);
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
        if (account is null)
        {
            if (!AnsiConsole.Confirm(ThisAssembly.Strings.GitHub.Login))
            {
                AnsiConsole.MarkupLine("[grey]-[/] Please run [yellow]gh auth login[/] to authenticate, [yellow]gh auth status -h github.com[/] to verify your status.");
                return false;
            }

            var process = System.Diagnostics.Process.Start("gh", "auth login");

            process.WaitForExit();
            if (process.ExitCode != 0)
                return false;

            account = Authenticate();
            if (account is null)
            {
                AnsiConsole.MarkupLine("[red]x[/] Could not retrieve authenticated user with GitHub CLI.");
                AnsiConsole.MarkupLine("[grey]-[/] Please run [yellow]gh auth login[/] to authenticate, [yellow]gh auth status -h github.com[/] to verify your status.");
                return false;
            }
        }

        return true;
    }

    public static AccountInfo? Authenticate()
    {
        if (!Process.TryExecute("gh", "auth status -h github.com", out var output) || output is null)
            return default;

        if (output.Contains("gh auth login"))
            return default;

        if (!Process.TryExecute("gh", "api user", out output) || output is null)
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
}
