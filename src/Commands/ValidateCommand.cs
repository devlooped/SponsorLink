using System.ComponentModel;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

[Description("Validates the active sponsorships manifest, if any")]
public partial class ValidateCommand : Command
{
    public override int Execute(CommandContext context)
    {
        if (Variables.Manifest is not string token)
        {
            AnsiConsole.MarkupLine("[red]x[/] No SponsorLink manifest found. Run [yellow]gh sponsors[/] to initialize it.");
            return -1;
        }

        try
        {
            var manifest = Manifest.Read(token);
            AnsiConsole.MarkupLine($"[green]✓[/] The manifest is valid, expires on {manifest.ExpiresAt:yyyy-MM-dd}.");
            return 0;
        }
        catch (SecurityTokenExpiredException)
        {
            AnsiConsole.MarkupLine("[red]x[/] The manifest has expired. Run [yellow]gh sponsors[/] to generate a new one.");
            return -2;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            AnsiConsole.MarkupLine("[red]x[/] The manifest signature is invalid. Run [yellow]gh sponsors[/] to generate a new one.");
            return -3;
        }
        catch (SecurityTokenException ex)
        {
            AnsiConsole.MarkupLine($"[red]x[/] The manifest is invalid. Run [yellow]gh sponsors[/] to generate a new one.");
            AnsiConsole.WriteException(ex);
            return -4;
        }
    }
}
