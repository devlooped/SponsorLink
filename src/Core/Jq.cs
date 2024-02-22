using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using CliWrap.Buffered;
using GraphQuery = (string Query, string? JQ);

namespace Devlooped.Sponsors;

/// <summary>
/// Executes JQ queries on JSON input.
/// </summary>
public static class Jq
{
    static JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
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

        if (query.JQ is { Length: > 0 })
            return await QueryAsync(await result.Content.ReadAsStringAsync(), query.JQ);

        return await result.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Execute the query and return the selected result.
    /// </summary>
    public static async Task<string> QueryAsync(string json, string query)
    {
        var jq = Path.Combine(AppContext.BaseDirectory, "lib",
            OperatingSystem.IsLinux() ? "jq-linux-amd64" : @"jq-win64.exe");

        Debug.Assert(File.Exists(jq));

        var jd = await Cli.Wrap(jq)
            .WithArguments(["-r", query])
            .WithStandardInputPipe(PipeSource.FromString(json))
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        return jd.StandardOutput.Trim();
    }
}
