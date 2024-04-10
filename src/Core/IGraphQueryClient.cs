using System.Text.Json;

namespace Devlooped.Sponsors;

/// <summary>
/// Represents a GraphQL query and optional JQ filter.
/// </summary>
public record GraphQuery(string Query, string? JQ = null);

/// <summary>
/// Usability overloads for executing GraphQL queries.
/// </summary>
public static class GraphQueryExtensions
{
    public static Task<string?> QueryAsync(this IGraphQueryClient client, string query, params (string name, object value)[] variables)
        => client.QueryAsync(new GraphQuery(query), variables);

    public static Task<string?> QueryAsync(this IGraphQueryClient client, string query, string jq, params (string name, object value)[] variables)
        => client.QueryAsync(new GraphQuery(query, jq), variables);

    public static Task<T?> QueryAsync<T>(this IGraphQueryClient client, string query, params (string name, object value)[] variables)
        => client.QueryAsync<T>(new GraphQuery(query), variables);

    public static Task<T?> QueryAsync<T>(this IGraphQueryClient client, string query, string jq, params (string name, object value)[] variables)
        => client.QueryAsync<T>(new GraphQuery(query, jq), variables);

    public static async Task<T?> QueryAsync<T>(this IGraphQueryClient client, GraphQuery query, params (string name, object value)[] variables)
    {
        if (await client.QueryAsync(query, variables) is { Length: > 0 } result)
            return JsonSerializer.Deserialize<T>(result, JsonOptions.Default);

        return default;
    }
}

/// <summary>
/// Provides a factory for creating <see cref="IGraphQueryClient"/> instances that use 
/// a certain named configuration (i.e. a named <see cref="HttpClient"/> via <see cref="IHttpClientFactory"/>).
/// </summary>
public interface IGraphQueryClientFactory
{
    /// <summary>
    /// Creates a new <see cref="IGraphQueryClient"/> instance using the specified name.
    /// </summary>
    /// <param name="name">The client name, typically matching a named <see cref="HttpClient"/> via <see cref="IHttpClientFactory"/>.</param>
    IGraphQueryClient Create(string name);
}

/// <summary>
/// Interface implemented by clients that execute a GraphQL GitHub query.
/// </summary>
public interface IGraphQueryClient
{
    /// <summary>
    /// Executes the query and returns the result as a strong, or null if the query failed.
    /// </summary>
    /// <param name="query">GraphQL query containing optional JQ filter.</param>
    /// <param name="variables">Optional variables that parameterize the query.</param>
    /// <returns>The query result as a string, or null if the query failed.</returns>
    Task<string?> QueryAsync(GraphQuery query, params (string name, object value)[] variables);
}