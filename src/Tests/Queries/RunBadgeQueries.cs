using System.Runtime.CompilerServices;
using Azure.Identity;
using Azure.Monitor.Query;

namespace Devlooped.SponsorLink;

public record RunBadgeQueries(ITestOutputHelper Output)
{
    [Fact]
    public Task users() => RunQuery();

    async Task RunQuery([CallerMemberName] string query = "")
    {
        var kql = File.ReadAllText(@$"Queries\{query}.kql");
        var creds = new DefaultAzureCredential();
        var client = new LogsQueryClient(creds);

        var result = await client.QueryWorkspaceAsync<long>(
            Constants.LogAnalyticsWorkspaceId, kql,
            QueryTimeRange.All);

        Output.WriteLine(result.Value.FirstOrDefault(-1).ToString());
    }
}
