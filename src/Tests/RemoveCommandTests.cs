using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Devlooped.Sponsors;
using DotNetConfig;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Spectre.Console.Cli;
using static Devlooped.Helpers;
using static Devlooped.Sponsors.Process;

namespace Devlooped.Tests;

[Collection("GitHub")]
public class RemoveCommandTests
{
    Config config = Config.Build();

    [SecretsFact("GitHub:Token")]
    public async Task RemoveInvokesHttpDeleteEndpoint()
    {
        EnsureAuthenticated("GitHub:Token");

        var graph = new Mock<IGraphQueryClient>();
        // Return default 'main' branch from GraphQueries.DefaultBranch
        graph.Setup(x => x.QueryAsync(GraphQueries.DefaultBranch("devlooped", ".github"))).ReturnsAsync("main");

        var command = new SyncCommand(
            Mock.Of<ICommandApp>(MockBehavior.Strict),
            config,
            graph.Object,
            new GitHubAppAuthenticator(Services.GetRequiredService<IHttpClientFactory>()),
            Services.GetRequiredService<IHttpClientFactory>());

        var result = await command.ExecuteAsync(new CommandContext(["sync"], Mock.Of<IRemainingArguments>(), "sync", null), new SyncCommand.SyncSettings
        {
            Sponsorable = ["devlooped"],
            // NOTE: would only succeed if we hav previously sync'ed at least once with the devlooped gh app
            // so that the access token is cached.
            Unattended = true,
        });

        Assert.Equal(0, result);

        var handler = new Mock<IHttpMessageHandler>(MockBehavior.Strict);
        handler.Setup(h => h.Send(It.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Delete && m.RequestUri!.PathAndQuery == "/me")))
            .Throws<NotImplementedException>();

        var removeCommand = new RemoveCommand(
            CreateMockedHttp(handler.Object),
            new GitHubAppAuthenticator(Services.GetRequiredService<IHttpClientFactory>()));

        await Assert.ThrowsAsync<NotImplementedException>(() => removeCommand.ExecuteAsync(new CommandContext(["remove", "devlooped"], Mock.Of<IRemainingArguments>(), "remove", null),
            new RemoveCommand.RemoveSettings { Sponsorable = ["devlooped"] }));
    }

    static IHttpClientFactory CreateMockedHttp(IHttpMessageHandler mock) => new ServiceCollection()
            .AddHttpClient()
            .ConfigureHttpClientDefaults(c => c.ConfigurePrimaryHttpMessageHandler(() => new MockHttpHandler(mock)))
            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();

    class MockHttpHandler(IHttpMessageHandler handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler.Send(request));
    }

    public interface IHttpMessageHandler
    {
        HttpResponseMessage Send(HttpRequestMessage request);
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
