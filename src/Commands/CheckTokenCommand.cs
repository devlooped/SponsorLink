using System.ComponentModel;
using System.Text.Json;
using DotNetConfig;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace Devlooped.Sponsors;

[Description("Checks the validity of a GitHub token.")]
public class CheckTokenCommand(IGraphQueryClient graph, Config config) : GitHubAsyncCommand<CheckTokenCommand.CheckSettings>(config)
{
    public class CheckSettings : ToSSettings
    {
        [Description(@"GitHub authentication token to check")]
        [CommandArgument(0, "<TOKEN>")]
        public required string Token { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CheckSettings settings)
    {
        using var withToken = GitHub.WithToken(settings.Token);
        // NOTE: gh cli will not set an invalid token, so we first need to check for that (null withToken)
        // before checking the base execute which will also authenticate.
        if (withToken is null || await base.ExecuteAsync(context, settings) is not 0)
        {
            AnsiConsole.MarkupLine(":cross_mark: [yellow]Invalid GitHub token provided[/]");
            return -1;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("gh", "auth status")
        {
        })?.WaitForExit();

        AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(await graph.QueryAsync(GraphQueries.RateLimits))));

        return 0;
    }
}