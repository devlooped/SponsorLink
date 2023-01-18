using Microsoft.Extensions.Configuration;
using Moq;
using ScenarioTests;

namespace Devlooped.SponsorLink;

public partial class SponsorsManagerTests : IDisposable
{
    TableConnection connection = new(CloudStorageAccount.DevelopmentStorageAccount, nameof(SponsorsManagerTests));

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
        var partition = DocumentPartition.Create<AccountEmail>(connection, includeProperties: true);
        await partition.PutAsync(new(new AccountId(1234, "asdf", "kzu"), "kzu@github.com"));

        Assert.NotNull(await partition.GetAsync("1234"));
    }

    [Scenario(NamingPolicy = ScenarioTestMethodNamingPolicy.Test)]
    public async Task AppUsage(ScenarioContext scenario)
    {
        var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        var repo = DocumentPartition.Create<Account>(connection, "Sponsorable", includeProperties: true);

        var manager = new SponsorsManager(
            Mock.Of<IHttpClientFactory>(),
            new SecurityManager(config),
            connection.StorageAccount, connection,
            Mock.Of<IEventStream>());

        var id = new AccountId(1234, "w_a", "kzu");

        await manager.AppInstallAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is inactive when first installed", async () =>
        {
            var account = await repo.GetAsync(id.Id.ToString());

            Assert.NotNull(account?.Id);
            Assert.Equal(AppState.Installed, account.State);
            Assert.False(account.IsActive);
        });

        scenario.Fact("Suspending non-installed app throws", async () =>
            await Assert.ThrowsAsync<ArgumentException>(() => manager.AppSuspendAsync(AppKind.Sponsorable, new AccountId(456, "asdf", "asdf")))
        );

        scenario.Fact("Uninstalled non-installed app throws", async () =>
            await Assert.ThrowsAsync<ArgumentException>(() => manager.AppUninstallAsync(AppKind.Sponsorable, new AccountId(456, "asdf", "asdf")))
        );

        await manager.AppSuspendAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is inactive and suspended when suspending", async () =>
        {
            var account = await repo.GetAsync(id.Id.ToString());

            Assert.Equal(AppState.Suspended, account!.State);
            Assert.False(account.IsActive);
        });

        await manager.AppUnsuspendAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is installed when unsuspended", async () =>
        {
            var account = await repo.GetAsync(id.Id.ToString());

            Assert.Equal(AppState.Installed, account!.State);
        });

        // Fake authorizing
        //await manager.AuthorizeAsync(AppKind.Sponsorable, 1234, "asdf");
        var entity = await repo.GetAsync(id.Id.ToString());
        Assert.NotNull(entity);
        await repo.PutAsync(entity with
        {
            AccessToken = "asdfasdf",
            Authorized = true,
        });

        await scenario.Fact("App is active when installed and authorized", async () =>
        {
            var account = await repo.GetAsync(id.Id.ToString());

            Assert.True(account!.IsActive);
        });

        await manager.AppUninstallAsync(AppKind.Sponsorable, id);

        await scenario.Fact("App is marked deleted when uninstalled", async () =>
        {
            var account = await repo.GetAsync(id.Id.ToString());

            Assert.Equal(AppState.Deleted, account!.State);
        });
    }

    [Scenario(NamingPolicy = ScenarioTestMethodNamingPolicy.Test)]
    public async Task SponsorsUsage(ScenarioContext scenario)
    {
        var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        var manager = new SponsorsManager(
            Mock.Of<IHttpClientFactory>(),
            new SecurityManager(config),
            connection.StorageAccount, connection,
            Mock.Of<IEventStream>());


    }
}
