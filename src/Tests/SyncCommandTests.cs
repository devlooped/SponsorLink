using Devlooped.Sponsors;
using DotNetConfig;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Spectre.Console.Cli;
using static Devlooped.Helpers;
using static Devlooped.Sponsors.Process;

namespace Devlooped.Tests;

[Collection("GitHub")]
public class SyncCommandTests
{
    Config config = Config.Build();

    [LocalFact]
    public async Task FirstRunWelcome()
    {
        config = config.Unset("sponsorlink", "firstrun");

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(x => x.Run(It.Is<IEnumerable<string>>(args => args.Contains("welcome"))) == -42),
            config,
            Mock.Of<IGraphQueryClient>(MockBehavior.Strict),
            Mock.Of<IGitHubAppAuthenticator>(MockBehavior.Strict),
            Mock.Of<IHttpClientFactory>(MockBehavior.Strict));

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), new SyncCommand.SyncSettings());

        Assert.Equal(-42, result);
    }

    [LocalFact]
    public async Task FirstRunWelcomeCompleted()
    {
        config = config.SetBoolean("sponsorlink", "firstrun", true);

        // By forcing an unauthenticated CLI, we can shortcircuit the execution at the login
        if (TryExecute("gh", "auth status", out var status))
            Assert.True(TryExecute("gh", "auth logout --hostname github.com", out var output));

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(MockBehavior.Strict),
            config,
            Mock.Of<IGraphQueryClient>(MockBehavior.Strict),
            Mock.Of<IGitHubAppAuthenticator>(MockBehavior.Strict),
            Mock.Of<IHttpClientFactory>(MockBehavior.Strict));

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), new SyncCommand.SyncSettings());

        // unauthenticated GH CLI
        Assert.Equal(-1, result);
    }

    [LocalFact("GitHub:Token")]
    public async Task NoSponsorableOrLocalDiscoveryRunsGraphDiscoveryViewerSponsored()
    {
        EnsureAuthenticated();

        var graph = new Mock<IGraphQueryClient>();
        // Force a null return so we can fail discovery from GraphQueries.ViewerSponsored (string[])
        graph.SetReturnsDefault<Task<string[]?>>(Task.FromResult<string[]?>(null));

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(MockBehavior.Strict),
            config,
            // This allows the graph client to not fail and return null, causing a discovery failure.
            graph.Object,
            Mock.Of<IGitHubAppAuthenticator>(MockBehavior.Strict),
            Mock.Of<IHttpClientFactory>(MockBehavior.Strict));

        var settings = new SyncCommand.SyncSettings
        {
            Unattended = true,
        };

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(-2, result);
    }

    [LocalFact("GitHub:Token")]
    public async Task NoSponsorableOrLocalDiscoveryRunsGraphDiscoveryViewerOrgs()
    {
        EnsureAuthenticated();

        var graph = new Mock<IGraphQueryClient>();
        // Force a null return so we can fail discovery from GraphQueries.ViewerOrganizations (Organization[])
        graph.SetReturnsDefault<Task<Organization[]?>>(Task.FromResult<Organization[]?>(null));

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(MockBehavior.Strict),
            config,
            // This allows the graph client to not fail and return null, causing a discovery failure.
            graph.Object,
            Mock.Of<IGitHubAppAuthenticator>(MockBehavior.Strict),
            Mock.Of<IHttpClientFactory>(MockBehavior.Strict));

        var settings = new SyncCommand.SyncSettings
        {
            Unattended = true,
        };

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(-2, result);
    }

    [LocalFact("GitHub:Token")]
    public async Task ExplicitSponsorableSync_NoSponsorableManifest()
    {
        EnsureAuthenticated();

        var graph = new Mock<IGraphQueryClient>();
        // Return default 'main' branch from GraphQueries.DefaultBranch
        graph.Setup(x => x.QueryAsync(GraphQueries.DefaultBranch("kzu", ".github"))).ReturnsAsync("main");

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(MockBehavior.Strict),
            config,
            graph.Object,
            Mock.Of<IGitHubAppAuthenticator>(MockBehavior.Strict),
            Services.GetRequiredService<IHttpClientFactory>());

        var settings = new SyncCommand.SyncSettings
        {
            Sponsorable = ["kzu"],
            Unattended = true,
        };

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(-3, result);
    }

    [LocalFact("GitHub:Token")]
    public async Task ExplicitSponsorableSync_InvalidSponsorableManifest()
    {
        EnsureAuthenticated();

        var graph = new Mock<IGraphQueryClient>();
        // Return default 'main' branch from GraphQueries.DefaultBranch
        graph.Setup(x => x.QueryAsync(GraphQueries.DefaultBranch("kzu", ".github"))).ReturnsAsync("sponsorlink");

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(MockBehavior.Strict),
            config,
            graph.Object,
            Mock.Of<IGitHubAppAuthenticator>(MockBehavior.Strict),
            Services.GetRequiredService<IHttpClientFactory>());

        var settings = new SyncCommand.SyncSettings
        {
            Sponsorable = ["kzu"],
            Unattended = true,
        };

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(-4, result);
    }

    [SecretsFact("GitHub:NonSponsoring")]
    public async Task ExplicitSponsorableSync_NonSponsoringUser()
    {
        EnsureAuthenticated("GitHub:NonSponsoring");

        var graph = new Mock<IGraphQueryClient>();
        // Return default 'main' branch from GraphQueries.DefaultBranch
        graph.Setup(x => x.QueryAsync(GraphQueries.DefaultBranch("devlooped", ".github"))).ReturnsAsync("main");

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(MockBehavior.Strict),
            config,
            graph.Object,
            new GitHubAppAuthenticator(Services.GetRequiredService<IHttpClientFactory>()),
            Services.GetRequiredService<IHttpClientFactory>());

        var settings = new SyncCommand.SyncSettings
        {
            Sponsorable = ["devlooped"],
            // NOTE: requires prior running of the `gh sponsors sync --namespace nonsponsoring` command for interactive auth.
            Namespace = "nonsponsoring",
            Unattended = true,
        };

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(-6, result);
    }

    void EnsureAuthenticated(string secret = "GitHub:Token")
    {
        if (!config.TryGetBoolean("sponsorlink", "firstrun", out var firstRunCompleted) || !firstRunCompleted)
            config = config.SetBoolean("sponsorlink", "firstrun", true);

        if (!TryExecute("gh", "auth status", out var status))
        {
            Assert.True(TryExecute("gh", "auth login --with-token", Configuration[secret]!, out var output));
            Assert.True(TryExecute("gh", "auth status", out status));
        }
    }
}
