using System.ComponentModel;
using DotNetConfig;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Executes the first-run experience")]
public class WelcomeCommand(ICommandApp app, Config config) : Command<WelcomeCommand.WelcomeSettings>
{
    public class WelcomeSettings : ToSSettings
    {
        [CommandOption("--welcome", IsHidden = true)]
        public bool Force { get; set; }
    }

    public override int Execute(CommandContext context, WelcomeSettings settings)
    {
        if (settings.ToS == true)
        {
            // Accept unattended.
            config.SetString("sponsorlink", "tos", "true");
            return 0;
        }

        AnsiConsole.Write(new Panel(new Rows(
            new Markup(ThisAssembly.Strings.FirstRun.What)))
        {
            Border = BoxBorder.None,
            Padding = new Padding(2, 1, 2, 0),
        });

        AnsiConsole.Write(new Panel(new Rows(
            new Rule(ThisAssembly.Strings.FirstRun.HowTitle).RuleStyle(Color.MediumPurple2).LeftJustified(),
            new Markup(ThisAssembly.Strings.FirstRun.How)))
        {
            Border = BoxBorder.None,
            Padding = new Padding(2, 1, 2, 0),
        });

        AnsiConsole.Write(new Panel(new Rows(
            new Rule(ThisAssembly.Strings.FirstRun.PrivacyTitle).RuleStyle(Color.MediumPurple2).LeftJustified(),
            new Markup(ThisAssembly.Strings.FirstRun.Privacy)))
        {
            Border = BoxBorder.None,
            Padding = new Padding(2, 1, 2, 0),
        });

        if (!AnsiConsole.Confirm(ThisAssembly.Strings.FirstRun.Acceptance))
        {
            return -1;
        }

        config.SetString("sponsorlink", "tos", "true");
        return 0;
    }
}
