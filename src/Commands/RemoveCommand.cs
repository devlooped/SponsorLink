using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

[Description("Removes all user data from the backend and local machine.")]
public class RemoveCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        if (await Session.AuthenticateAsync() is not { } principal ||
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value is not string id)
        {
            AnsiConsole.MarkupLine(ThisAssembly.Strings.Remove.AuthenticationRequired);
            return -1;
        }

        // NOTE: to test the local flow end to end, run the SponsorLink functions App project locally. You will 
        var url = Debugger.IsAttached ? "http://localhost:7288/remove" : "https://sponsorlink.devlooped.com/remove";

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Variables.AccessToken);

        var response = await AnsiConsole.Status().StartAsync(ThisAssembly.Strings.Remove.Deleting, async _
            => await http.PostAsync(url, null));

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            AnsiConsole.MarkupLine(ThisAssembly.Strings.Remove.AuthenticationRequired);
            return -1;
        }
        else if (!response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine(ThisAssembly.Strings.Remove.ReportIssue);
            return -1;
        }

        AnsiConsole.MarkupLine(ThisAssembly.Strings.Remove.Deleted);
        Variables.Clear();

        return 0;
    }
}
