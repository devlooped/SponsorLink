using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Devlooped.SponsorLink.GraphQL;

public record SponsorsResult(SponsorsData Data);
public record SponsorsData(Sponsorable? Organization, Sponsorable? User);
public record Sponsorable(string Id, string Login, Sponsorships SponsorshipsAsMaintainer);
public record Sponsorships(Sponsorship[] Nodes);
public record Sponsorship(DateTime CreatedAt, bool IsOneTimePayment, Sponsor SponsorEntity, Tier Tier);
public record Sponsor(string Id, string Login);
public record Tier(int MonthlyPriceInDollars);

public record ManualSponsors(ITestOutputHelper Output)
{
    [LocalFact]
    public async Task KzuSponsorsDevlooped()
    {
        var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        if (!CloudStorageAccount.TryParse(config["ProductionStorageAccount"], out var storage))
        {
            Output.WriteLine("Did not find 'ProductionStorageAccount' secret. Cannot run kzu>devlooped sponsorship");
            return;
        }

        var manager = new SponsorsManager(
            Mock.Of<IHttpClientFactory>(),
            new SecurityManager(config),
            new TableConnection(storage, nameof(SponsorLink)),
            Mock.Of<IEventStream>(),
            new SponsorsRegistry(storage, Mock.Of<IEventStream>()));

        // My account info from https://docs.github.com/en/graphql/overview/explorer
        await manager.SponsorAsync(Constants.DevloopedAccount,
            new AccountId("MDQ6VXNlcjE2OTcwNw==", "kzu"), 10);

        await manager.SponsorAsync(Constants.DevloopedAccount,
            new AccountId("MDQ6VXNlcjUyNDMxMDY0", "anycarvallo"), 10);
    }

    /// <summary>
    /// Run this test manually to initialize a sponsorable account that is being onboarded to 
    /// SponsorLink which has existing sponsors. This is because for now we don't have a way 
    /// to query existing sponsors via GraphQL, so we need to manually initialize the account 
    /// with a JSON executed by the customer himself.
    /// </summary>
    [InlineData("")]
    [LocalTheory]
    public async Task SponsorableInit(string fileName)
    {
        // NOTE: set the file name/path to the JSON file with the sponsors data.
        if (string.IsNullOrEmpty(fileName))
            return;

        Assert.True(File.Exists(fileName), $"File path '{fileName}' does not exist.");

        var json = File.ReadAllText(fileName);
        var result = JsonSerializer.Deserialize<SponsorsResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);

        var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        if (!CloudStorageAccount.TryParse(config["ProductionStorageAccount"], out var account))
            Assert.Fail("Did not find a user secret named 'ProductionStorageAccount' to run the query against.");

        var events = new EventStream(config, new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration());

        var manager = new SponsorsManager(
            Mock.Of<IHttpClientFactory>(),
            new SecurityManager(config),
            new TableConnection(account, "SponsorLink"),
            //new EventStream(config, new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration()),
            events,
            new SponsorsRegistry(account, events));

        if (result.Data.Organization != null)
        {
            var sponsorable = new AccountId(result.Data.Organization.Id, result.Data.Organization.Login);
            foreach (var sponsorship in result.Data.Organization.SponsorshipsAsMaintainer.Nodes)
            {
                var sponsor = new AccountId(sponsorship.SponsorEntity.Id, sponsorship.SponsorEntity.Login);
                var note = $"Existing sponsor {sponsor.Login} for {sponsorable.Login} with ${sponsorship.Tier.MonthlyPriceInDollars} {(sponsorship.IsOneTimePayment ? "one-time" : "monthly")} payment";

                await manager.SponsorAsync(sponsorable, sponsor,
                    sponsorship.Tier.MonthlyPriceInDollars,
                    sponsorship.IsOneTimePayment ? DateOnly.FromDateTime(sponsorship.CreatedAt.AddDays(30)) : null,
                    note);
            }
        }
    }
}
