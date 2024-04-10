using System.Text.Json;

namespace Devlooped.Sponsors;

public class CliGraphQueryClient : IGraphQueryClient
{
    public Task<string?> QueryAsync(GraphQuery query, params (string name, object value)[] variables)
    {
        var args = $"api graphql -f query=\"{query.Query}\"";
        if (query.JQ?.Length > 0)
            args += $" --jq \"{query.JQ.Trim()}\"";

        foreach (var (name, value) in variables)
            args += $" -f {name}={System.Text.Json.JsonSerializer.Serialize(value)}";

        if (Process.TryExecute("gh", args, out var result))
            return Task.FromResult(result);

        return Task.FromResult<string?>(null);
    }
}
