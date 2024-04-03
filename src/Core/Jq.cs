using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GraphQuery = (string Query, string? JQ);

namespace Devlooped.Sponsors;

/// <summary>
/// Executes JQ queries on JSON input.
/// </summary>
public static class Jq
{
    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new JsonStringEnumConverter<AccountType>()
        }
    };

    public static async Task<T?> QueryAsync<T>(this HttpClient http, GraphQuery query)
        => JsonSerializer.Deserialize<T>(await QueryAsync(http, query), options);

    public static async Task<string> QueryAsync(this HttpClient http, GraphQuery query)
    {
        var result = await http.PostAsJsonAsync("graphql", new
        {
            query = query.Query
        });

        if (query.JQ?.Length > 0)
            return await JQ.ExecuteAsync(await result.Content.ReadAsStringAsync(), query.JQ);

        return await result.Content.ReadAsStringAsync();
    }
}
