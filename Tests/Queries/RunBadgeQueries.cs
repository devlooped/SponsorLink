using System.Runtime.CompilerServices;
using Azure.Identity;
using Azure.Monitor.Query;
using Microsoft.Extensions.Configuration;

namespace Devlooped.SponsorLink;

#pragma warning disable xUnit1013 // Public method should be marked as test
public record RunBadgeQueries(ITestOutputHelper Output)
#pragma warning restore xUnit1013 // Public method should be marked as test
{
    [Fact]
    public Task users() => RunQuery();

    async Task RunQuery([CallerMemberName] string query = "")
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(ThisAssembly.Project.UserSecretsId)
            .Build();

        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", config["AZURE_CLIENT_ID"]);
        Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", config["AZURE_CLIENT_SECRET"]);
        Environment.SetEnvironmentVariable("AZURE_TENANT_ID", config["AZURE_TENANT_ID"]);

        var kql = File.ReadAllText(@$"Queries\{query}.kql");
        var creds = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeAzureCliCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeInteractiveBrowserCredential = true,
            ExcludeManagedIdentityCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = true
        });
        var client = new LogsQueryClient(creds);

        var result = await client.QueryWorkspaceAsync<long>(
            Constants.LogAnalyticsWorkspaceId, kql,
            QueryTimeRange.All);

        Output.WriteLine(result.Value.FirstOrDefault(-1).ToString());
    }
}
