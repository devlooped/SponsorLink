using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Std;

namespace Devlooped.Sponsors;

public class CliGraphQueryClient : IGraphQueryClient
{
    public async Task<T?> QueryAsync<T>(GraphQuery<T> query, params (string name, object value)[] variables)
    {
        var vars = new Dictionary<string, object>(query.Variables);
        foreach (var (name, value) in variables)
            vars[name] = value;

        var args = query.IsLegacy ?
            "api " + UriTemplate.Expand(query.Query, vars) :
            $"api graphql -f query=\"{query.Query}\" " +
            string.Join(" ", vars.Select(x => $"-F {x.Key}={JsonSerializer.Serialize(x.Value)}"));

        if (query.JQ?.Length > 0)
            args += $" --jq \"{query.JQ.Trim()}\"";

        var success = Process.TryExecute("gh", args, out var result);
        if (result is null)
            return default;

        // Some GraphQL queries may return an error but also a useful payload we can JQ-over too.
        // See if the result is a JSON object with some data, and not a pure error. 
        // NOTE: the JQ command is not processed by the GH CLI in this case, so we must apply it ourselves.
        // This matches the behavior of the HttpGraphQueryClient.
        if (!success && !query.IsLegacy && 
            await JsonNode.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(result))) is JsonObject json && 
            json.TryGetPropertyValue("data", out _))
        {
            if (query.JQ?.Length > 0)
                result = await JQ.ExecuteAsync(result, query.JQ);

            // Consider the query a success if we got a data payload, even if the GH CLI returned an error.
            success = true;
        }

        if (success)
        {
            if (typeof(T) == typeof(string))
                return (T?)(object)result;

            if (string.IsNullOrEmpty(result))
                return default;

            return JsonSerializer.Deserialize<T>(result, JsonOptions.Default);
        }

        return default;
    }
}
