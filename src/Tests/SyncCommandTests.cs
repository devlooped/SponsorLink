using Devlooped.Sponsors;
using DotNetConfig;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Spectre.Console.Cli;
using static Devlooped.Helpers;
using static Devlooped.Sponsors.Process;

namespace Devlooped.Tests;

[Collection("GitHub")]
public class SyncCommandTests
{
    Config config = Config.Build().SetBoolean("sponsorlink", "tos", true);

    [LocalFact("GitHub:Token")]
    public async Task NoSponsorableOrLocalDiscoveryRunsGraphDiscoveryViewerSponsored()
    {
        EnsureAuthenticated();

        var graph = new Mock<IGraphQueryClient>();
        // Force a null return so we can fail discovery from GraphQueries.ViewerSponsored (string[])
        graph.SetReturnsDefault<Task<string[]?>>(Task.FromResult<string[]?>(null));

        var command = new SyncCommand(
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

        Assert.Equal(SyncCommand.ErrorCodes.GraphDiscoveryFailure, result);
    }

    [LocalFact("GitHub:Token")]
    public async Task NoSponsorableOrLocalDiscoveryRunsGraphDiscoveryViewerOrgs()
    {
        EnsureAuthenticated();

        var graph = new Mock<IGraphQueryClient>();
        // Force a null return so we can fail discovery from GraphQueries.ViewerOrganizations (Organization[])
        graph.SetReturnsDefault<Task<Organization[]?>>(Task.FromResult<Organization[]?>(null));

        var command = new SyncCommand(
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

        Assert.Equal(SyncCommand.ErrorCodes.GraphDiscoveryFailure, result);
    }

    [LocalFact("GitHub:Token")]
    public async Task ExplicitSponsorableSync_NoSponsorableManifest()
    {
        EnsureAuthenticated();

        var graph = new Mock<IGraphQueryClient>();
        var auth = new Mock<IGitHubAppAuthenticator>(MockBehavior.Strict);
        auth.Setup(x => x.AuthenticateAsync(It.IsAny<string>(), It.IsAny<IProgress<string>>(), false, "com.devlooped", null))
            .ReturnsAsync(Configuration["GitHub:Token"]);

        var command = new SyncCommand(
            config,
            graph.Object,
            auth.Object,
            Services.GetRequiredService<IHttpClientFactory>());

        var settings = new SyncCommand.SyncSettings
        {
            Sponsorable = ["kzu"],
            Unattended = true,
        };

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(SyncCommand.ErrorCodes.SponsorableManifestNotFound, result);
    }

    [LocalFact("GitHub:Token")]
    public async Task ExplicitSponsorableSync_InvalidSponsorableManifestNonMainBranch()
    {
        EnsureAuthenticated();

        var command = new SyncCommand(
            config,
            Mock.Of<IGraphQueryClient>(MockBehavior.Strict),
            Mock.Of<IGitHubAppAuthenticator>(MockBehavior.Strict),
            Services.GetRequiredService<IHttpClientFactory>());

        var settings = new SyncCommand.SyncSettings
        {
            Sponsorable = ["devlooped-bot"],
            Unattended = true,
        };

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(SyncCommand.ErrorCodes.SponsorableManifestInvalid, result);
    }

    [SecretsFact("GitHub:NonSponsoring")]
    public async Task ExplicitSponsorableSync_NonSponsoringUser()
    {
        using var auth = GitHub.WithToken(Configuration["GitHub:NonSponsoring"]);

        var graph = new Mock<IGraphQueryClient>();
        // Return default 'main' branch from GraphQueries.DefaultBranch
        graph.Setup(x => x.QueryAsync(GraphQueries.DefaultBranch("devlooped", ".github"))).ReturnsAsync("main");

        var command = new SyncCommand(
            config,
            graph.Object,
            new GitHubAppAuthenticator(Services.GetRequiredService<IHttpClientFactory>()),
            Services.GetRequiredService<IHttpClientFactory>());

        var settings = new SyncCommand.SyncSettings
        {
            Sponsorable = ["devlooped"],
            // NOTE: requires prior running of the `sponsor sync --namespace nonsponsoring` command for interactive auth.
            Namespace = "nonsponsoring",
            Unattended = true,
        };

        var manifestFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink", "github", "devlooped.jwt");
        if (File.Exists(manifestFile))
            File.Delete(manifestFile);

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), settings);

        Assert.Equal(SyncCommand.ErrorCodes.NotSponsoring, result);
    }

    void EnsureAuthenticated(string secret = "GitHub:Token")
    {
        if (!config.TryGetBoolean("sponsorlink", "tos", out var tosAccepted) || !tosAccepted)
            config = config.SetBoolean("sponsorlink", "tos", true);

        if (!TryExecute("gh", "auth status", out var status))
        {
            Assert.True(TryExecute("gh", "auth login --with-token", Configuration[secret]!, out var output));
            Assert.True(TryExecute("gh", "auth status", out status));
        }
    }
}
