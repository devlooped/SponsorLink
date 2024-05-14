using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

[Description("Executes the first-run experience")]
public class WelcomeCommand(ICommandApp app) : Command<WelcomeCommand.WelcomeSettings>
{
    public class WelcomeSettings : CommandSettings
    {
        [CommandOption("--welcome", IsHidden = true)]
        public bool Force { get; set; }
    }

    public override int Execute(CommandContext context, WelcomeSettings settings)
    {
        AnsiConsole.Write(new Panel(new Rows(
            new Rule(ThisAssembly.Strings.FirstRun.Welcome).RuleStyle(Color.Purple_2),
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

        Variables.FirstRunCompleted = true;

        if (AnsiConsole.Confirm(ThisAssembly.Strings.FirstRun.SyncNow))
        {
            return app.Run(["sync"]);
        }

        return 0;
    }
}
