using Microsoft.Extensions.Configuration;
using Moq;
using ScenarioTests;

namespace Devlooped.SponsorLink;

public sealed partial class SponsorsManagerTests : IDisposable
{
    readonly TableConnection connection = new(CloudStorageAccount.DevelopmentStorageAccount, nameof(SponsorsManagerTests));

    public ITestOutputHelper Output { get; }

    public SponsorsManagerTests(ITestOutputHelper output)
    {
        Output = output;
        // Ensure table is created;
        _ = connection.GetTableAsync().Result;
    }
    
    public void Dispose() => connection.StorageAccount.CreateTableServiceClient().DeleteTable(connection.TableName);

    [Fact(Skip = "Manual run")]
    public async Task DeleteDevelopmentTables()
    {
        var client = CloudStorageAccount.DevelopmentStorageAccount.CreateTableServiceClient();
        await foreach (var table in client.QueryAsync())
            await client.DeleteTableAsync(table.Name);
    }

    [Fact]
    public async Task SaveEmail()
    {
        var byEmail = TableRepository.Create<AccountEmail>(
            CloudStorageAccount.DevelopmentStorageAccount,
            partitionKey: x => x.Email,
            rowKey: x => x.Account);

        var byAccount = TableRepository.Create<AccountEmail>(
            CloudStorageAccount.DevelopmentStorageAccount,
            partitionKey: x => x.Account,
            rowKey: x => x.Email);

        var account = new AccountEmail("asdf", "kzu", "kzu@github.com");

        await byEmail.PutAsync(account);
        await byAccount.PutAsync(account);

        Assert.NotNull(await byEmail.GetAsync("kzu@github.com", "asdf"));
        Assert.NotNull(await byAccount.GetAsync("asdf", "kzu@github.com"));
    }

    [Scenario(NamingPolicy = ScenarioTestMethodNamingPolicy.Test)]
    public async Task AppUsage(ScenarioContext scenario)
    {
        var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        var repo = TablePartition.Create<Installation>(connection, "Sponsorable");

        var manager = new SponsorsManager(
            Mock.Of<IHttpClientFactory>(),
            new SecurityManager(config),
            connection.StorageAccount, connection,
            Mock.Of<IEventStream>(), 
            new SponsorsRegistry(connection.StorageAccount));

        var id = new AccountId("1234", "kzu");

        await manager.AppInstallAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is inactive when first installed", async () =>
        {
            var account = await repo.GetAsync(id.Id);

            Assert.NotNull(account);
            Assert.Equal(AppState.Installed, account.State);
        });

        scenario.Fact("Suspending non-installed app throws", async () =>
            await Assert.ThrowsAsync<ArgumentException>(() => manager.AppSuspendAsync(AppKind.Sponsorable, new AccountId("456", "asdf")))
        );

        scenario.Fact("Uninstalled non-installed app throws", async () =>
            await Assert.ThrowsAsync<ArgumentException>(() => manager.AppUninstallAsync(AppKind.Sponsorable, new AccountId("456", "asdf")))
        );

        await manager.AppSuspendAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is inactive and suspended when suspending", async () =>
        {
            var account = await repo.GetAsync(id.Id);

            Assert.Equal(AppState.Suspended, account!.State);
        });

        await manager.AppUnsuspendAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is installed when unsuspended", async () =>
        {
            var account = await repo.GetAsync(id.Id);

            Assert.Equal(AppState.Installed, account!.State);
        });

        await manager.AppUninstallAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is marked deleted when uninstalled", async () =>
        {
            var account = await repo.GetAsync(id.Id);

            Assert.Equal(AppState.Deleted, account!.State);
        });
    }

    [Scenario(NamingPolicy = ScenarioTestMethodNamingPolicy.Test)]
    public async Task SponsorsUsage(ScenarioContext scenario)
    {
        //var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        //var manager = new SponsorsManager(
        //    Mock.Of<IHttpClientFactory>(),
        //    new SecurityManager(config),
        //    connection.StorageAccount, connection,
        //    Mock.Of<IEventStream>());

        await Task.CompletedTask;
    }
}
