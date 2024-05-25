using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Std;

namespace Devlooped.Sponsors;

/// <summary>
/// Extensions for configuring the <see cref="IGraphQueryClient"/> in the dependency injection container.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GraphQueryClientExtensions
{
    /// <summary>
    /// Adds an HTTP-based <see cref="IGraphQueryClient"/> implementation to the service collection.
    /// </summary>
    public static IServiceCollection AddGraphQueryClient(this IServiceCollection services)
        => services.AddSingleton<IGraphQueryClientFactory, GraphQueryClientFactory>();

    class GraphQueryClientFactory(IHttpClientFactory http) : IGraphQueryClientFactory
    {
        public IGraphQueryClient CreateClient(string name) => new HttpGraphQueryClient(http, name);
    }
}

public class HttpGraphQueryClient(IHttpClientFactory factory, string name) : IGraphQueryClient
{
    public Task<T?> QueryAsync<T>(GraphQuery<T> query, params (string name, object value)[] variables)
    {
        // We don't support pagination for non-GraphQL queries (for now?)
        var paginate = !query.IsLegacy && typeof(T).IsArray && query.Query.Contains("$endCursor") && query.Query.Contains("first:");
        if (!paginate)
            return QuerySimpleAsync(query, variables);

        return QueryPaginatedArrayAsync(query, variables);
    }

    async Task<T?> QuerySimpleAsync<T>(GraphQuery<T> query, params (string name, object value)[] variables)
    {
        using var http = factory.CreateClient(name);

        var vars = new Dictionary<string, object>(query.Variables);
        foreach (var (name, value) in variables)
            vars[name] = value;

        var response = query.IsLegacy ?
            await http.GetAsync(UriTemplate.Expand("https://api.github.com" + query.Query, vars)) :
            await http.PostAsJsonAsync("https://api.github.com/graphql", new
            {
                query = query.Query,
                variables = vars
            });

        if (!response.IsSuccessStatusCode)
            return default;

        var json = query.JQ?.Length > 0 ?
            await JQ.ExecuteAsync(await response.Content.ReadAsStringAsync(), query.JQ) :
            await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(json) || json == "null")
            return default;

        if (typeof(T) == typeof(string))
            return (T)(object)json;

        // Primitive types that can be converted from string, return converted.
        var converter = TypeDescriptor.GetConverter(typeof(T));
        if (converter.CanConvertFrom(typeof(string)))
            return (T?)converter.ConvertFromString(json);

        return JsonSerializer.Deserialize<T>(json, JsonOptions.Default);
    }

    async Task<T?> QueryPaginatedArrayAsync<T>(GraphQuery<T> query, params (string name, object value)[] variables)
    {
        using var http = factory.CreateClient(name);

        var vars = new Dictionary<string, object>(query.Variables);
        foreach (var (name, value) in variables)
            vars[name] = value;

        var items = new List<object>();
        var info = default(PageInfo);

        while (true)
        {
            if (info != null)
                vars["endCursor"] = info.EndCursor;

            var response = await http.PostAsJsonAsync("https://api.github.com/graphql", new
            {
                query = query.Query,
                variables = vars
            });

            if (!response.IsSuccessStatusCode)
                break;

            var raw = await response.Content.ReadAsStringAsync();
            var data = query.JQ?.Length > 0 ?
                await JQ.ExecuteAsync(raw, query.JQ) :
                raw;

            if (string.IsNullOrEmpty(data))
                break;

            var typed = JsonSerializer.Deserialize<T>(data, JsonOptions.Default);
            if (typed is IEnumerable<object> array)
                items.AddRange(array);

            info = JsonSerializer.Deserialize<PageInfo>(await JQ.ExecuteAsync(raw, ".. | .pageInfo? | values"), JsonOptions.Default);
            if (info is null || !info.HasNextPage)
                break;
        }

        return (T?)(object)items.ToArray().Cast(typeof(T).GetElementType()!);
    }

    record PageInfo(bool HasNextPage, string EndCursor);
}
