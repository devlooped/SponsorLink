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
    public async Task<T?> QueryAsync<T>(GraphQuery<T> query, params (string name, object value)[] variables)
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

        if (typeof(T) == typeof(string))
            return (T)(object)json;

        if (string.IsNullOrEmpty(json))
            return default;
        
        return JsonSerializer.Deserialize<T>(json, JsonOptions.Default);
    }
}
