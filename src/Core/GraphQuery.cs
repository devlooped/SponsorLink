using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Devlooped.Sponsors;

/// <summary>
/// Represents a GraphQL query and optional JQ filter.
/// </summary>
[DebuggerDisplay("{JQ}")]
public class GraphQuery<T>(string query, string? jq = null)
{
    // Private variable to aid while debugging, to easily copy/paste to the CLI the 
    // various invocation styles for a given query.
    [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
    GraphQueryDebugger CLI => new(query, jq, Variables);

    /// <summary>
    /// The GraphQL query to execute.
    /// </summary>
    public string Query => query;
    /// <summary>
    /// The optional JQ filter to apply to the query result.
    /// </summary>
    public string? JQ => jq;
    /// <summary>
    /// Optional variables used in the query.
    /// </summary>
    public Dictionary<string, object> Variables { get; private set; } = [];

    /// <summary>
    /// Legacy queries use the older REST endpoints rather than the GraphQL API.
    /// </summary>
    public bool IsLegacy { get; set; }

    public override int GetHashCode() => HashCode.Combine(Query, JQ);

    public override bool Equals(object? obj) => obj is GraphQuery<T> other && Query == other.Query && JQ == other.JQ;

    class GraphQueryDebugger
    {
        public GraphQueryDebugger(string query, string? jq, Dictionary<string, object> variables)
        {
            http = variables.Count > 0 ?
            JsonSerializer.Serialize(new
            {
                query,
                variables
            }, JsonOptions.Default) :
            JsonSerializer.Serialize(new { query }, JsonOptions.Default);

            var sb = new StringBuilder();
            sb.Append("gh api graphql");

            foreach (var (name, value) in variables)
                sb.Append($" -F {name}={JsonSerializer.Serialize(value)}");

            sb.Append(" -f query='").Append(query).Append('\'');

            if (jq?.Length > 0)
                sb.Append(" --jq '").Append(jq).Append('\'');

            github = sb.ToString();

            sb.Clear();
            sb.Append("curl -X POST -H \"Authorization: Bearer $(gh auth token)\" -d '");
            sb.Append(http).Append("' https://api.github.com/graphql | convertfrom-json | convertto-json -depth 10");

            if (jq?.Length > 0)
                sb.Append(" | %{ write-host $_; $_ } | jq -r '").Append(jq).Append('\'');

            curl = sb.ToString();
        }

        /// <summary>
        /// Raw HTTP request body.
        /// </summary>
        public string http { get; }
        /// <summary>
        /// GH CLI command.
        /// </summary>
        public string github { get; }
        /// <summary>
        /// PWSH curl + jq command.
        /// </summary>
        public string curl { get; }
    }
}

/// <summary>
/// A query that returns a typed result.
/// </summary>
public class GraphQuery(string query, string? jq = null) : GraphQuery<string>(query, jq)
{
}