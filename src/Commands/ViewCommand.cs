using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JWT.Builder;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

[Description("View the information the backend contains about the authenticated user and the local manifest")]
public class ViewCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        if (Variables.AccessToken is string token)
        {
            Render("Auth0 Token", token);
        }
        else
        {
            // Authenticated user must match GH user
            var principal = await Session.AuthenticateAsync();
            if (principal == null)
                return -1;

            Render("Auth0 Token", Variables.AccessToken);
        }

        if (Variables.Manifest is string manifest)
        {
            Render("SponsorLink Manifest", manifest);
        }

        return 0;
    }

    int Render(string header, string? token)
    {
        if (token is null)
            return -1;

        var json = JwtBuilder.Create().DoNotVerifySignature().Decode(token);

        AnsiConsole.Write(
            new Panel(new JsonText(json))
                .Header(header)
                .Collapse()
                .RoundedBorder()
                .BorderColor(Color.Green));

        return 0;
    }
}
