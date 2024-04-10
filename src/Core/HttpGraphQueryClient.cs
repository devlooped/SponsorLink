using System.ComponentModel;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

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
        public IGraphQueryClient Create(string name) => new HttpGraphQueryClient(http, name);
    }
}

public class HttpGraphQueryClient(IHttpClientFactory factory, string name) : IGraphQueryClient
{
    public async Task<string?> QueryAsync(GraphQuery query, params (string name, object value)[] variables)
    {
        using var http = factory.CreateClient(name);

        var result = await http.PostAsJsonAsync("https://api.github.com/graphql", new
        {
            query = query.Query,
            variables = variables.DistinctBy(x => x.name).ToDictionary(x => x.name, x => x.value)
        });

        if (!result.IsSuccessStatusCode)
            return null;

        if (query.JQ?.Length > 0)
            return await JQ.ExecuteAsync(await result.Content.ReadAsStringAsync(), query.JQ);

        return await result.Content.ReadAsStringAsync();
    }
}
