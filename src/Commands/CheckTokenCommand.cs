using System.ComponentModel;
using System.Text.Json;
using DotNetConfig;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace Devlooped.Sponsors;

public class CheckTokenCommand(IGraphQueryClient graph, ICommandApp app, Config config) : GitHubAsyncCommand<CheckTokenCommand.CheckSettings>(app, config)
{
    public class CheckSettings : ToSSettings
    {
        [Description(@"GitHub authentication token to check")]
        [CommandArgument(0, "<TOKEN>")]
        public required string Token { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CheckSettings settings)
    {
        if (await base.ExecuteAsync(context, settings) is not 0)
            return -1;

        using var withToken = GitHub.WithToken(settings.Token);

        if (withToken is null)
        {
            AnsiConsole.MarkupLine("[red]Invalid token[/]");
            return -1;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("gh", "auth status")
        {
        })?.WaitForExit();

        AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(await graph.QueryAsync(GraphQueries.RateLimits))));

        return 0;
    }
}