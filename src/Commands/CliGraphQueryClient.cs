using System.ComponentModel;
using System.Runtime.CompilerServices;
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
            $"api graphql -f query=\"{query.Query.Replace("\"", "\\\"")}\" " +
            string.Join(" ", vars.Select(x => $"-F {x.Key}={JsonSerializer.Serialize(x.Value)}"));

        if (query.JQ?.Length > 0)
            args += $" --jq \"{query.JQ.Trim().Replace("\"", "\\\"")}\"";

        // For now, we only add support for auto-pagination of array results.
        var paginate = typeof(T).IsArray && query.Query.Contains("$endCursor") && query.Query.Contains("first:");
        if (paginate)
            args += " --paginate";

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
            if (string.IsNullOrEmpty(result) || result == "null")
                return default;

            if (typeof(T) == typeof(string))
                return (T?)(object)result;

            // Primitive types that can be converted from string, return converted.
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter.CanConvertFrom(typeof(string)))
                return (T?)converter.ConvertFromString(result);

            if (paginate && typeof(T).IsArray)
            {
                var items = new List<object>();
                foreach (var array in result.Split('[', StringSplitOptions.RemoveEmptyEntries).Select(x => JsonSerializer.Deserialize<T>("[" + x, JsonOptions.Default)))
                {
                    if (array is IEnumerable<object> elements)
                        items.AddRange(elements);
                }
                
                // Convert the object list to the destination array type.
                var typed = Array.CreateInstance(typeof(T).GetElementType()!, items.Count);
                Array.Copy(items.ToArray(), typed, items.Count);
                return (T?)(object)typed;
            }

            return JsonSerializer.Deserialize<T>(result, JsonOptions.Default);
        }

        return default;
    }


}
