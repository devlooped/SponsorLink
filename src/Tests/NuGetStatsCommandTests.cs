using System.Text.Json;
using Devlooped.Sponsors;
using DotNetConfig;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Spectre.Console.Cli;
using static Devlooped.Helpers;
using static Devlooped.Sponsors.Process;

namespace Devlooped.Tests;

[Collection("GitHub")]
public class NuGetStatsCommandTests
{
    Config config = Config.Build().SetBoolean("sponsorlink", "tos", true);

    [LocalFact("GitHub:Token")]
    public async Task CollectsStatsForMerq()
    {
        EnsureAuthenticated();

        var command = new NuGetStatsCommand(
            config,
            new HttpGraphQueryClient(Services.GetRequiredService<IHttpClientFactory>(), "GitHub"),
            Services.GetRequiredService<IHttpClientFactory>());

        var settings = new NuGetStatsCommand.NuGetStatsSettings
        {
            Owner = "devlooped",
            Force = true,
            PackageId = "Merq",
        };

        try
        {
            var result = await command.ExecuteAsync(
                new CommandContext(["nuget-stats"], Mock.Of<IRemainingArguments>(), "nuget-stats", null),
                settings,
                CancellationToken.None);

            Assert.Equal(0, result);

            var json = File.ReadAllText("devlooped.json");
            var model = JsonSerializer.Deserialize<OpenSource>(json, JsonOptions.Default);

            Assert.NotNull(model);
            Assert.True(model.Packages.Values.Any(p => p.ContainsKey("Merq")),
                "Expected the Merq package to be present in the devlooped owner stats.");
        }
        finally
        {
            if (File.Exists("devlooped.json"))
                File.Delete("devlooped.json");
        }
    }

    void EnsureAuthenticated(string secret = "GitHub:Token")
    {
        if (!config.TryGetBoolean("sponsorlink", "tos", out var tosAccepted) || !tosAccepted)
            config = config.SetBoolean("sponsorlink", "tos", true);

        if (!TryExecute("gh", "auth status", out var _))
        {
            Assert.True(TryExecute("gh", "auth login --with-token", Configuration[secret]!, out var output));
            Assert.True(TryExecute("gh", "auth status", out _));
        }
    }
}
