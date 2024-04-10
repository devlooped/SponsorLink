using System.Text.Json;

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

    public static bool TryQuery(GraphQuery query, out string? result, params (string name, string value)[] fields)
        => TryQuery(query.Query, query.JQ, out result, fields);

    public static bool TryQuery(string query, string? jq, out string? result, params (string name, string value)[] fields)
    {
        var args = $"api graphql -f query=\"{query}\"";
        if (jq?.Length > 0)
            args += $" --jq \"{jq.Trim()}\"";

        foreach (var (name, value) in fields)
            args += $" -f {name}={value}";

        return Process.TryExecute("gh", args, out result);
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
