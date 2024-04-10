using System.ComponentModel;
using System.Runtime.CompilerServices;
using Spectre.Console;
using Spectre.Console.Cli;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

[Description("Executes the first-run experience")]
public class WelcomeCommand(ICommandApp app, AccountInfo user) : Command
{
    public override int Execute(CommandContext context)
    {
        AnsiConsole.Write(new Panel(new Rows(
            new Rule(ThisAssembly.Strings.FirstRun.Welcome).RuleStyle(Color.Purple_2),
            new Markup(ThisAssembly.Strings.FirstRun.What)))
        {
            Border = BoxBorder.None,
            Padding = new Padding(2, 1, 2, 1),
        });

        AnsiConsole.Write(new Panel(new Rows(
            new Rule(ThisAssembly.Strings.FirstRun.HowTitle).RuleStyle(Color.MediumPurple2).LeftJustified(),
            new Markup(ThisAssembly.Strings.FirstRun.How(user.Login))))
        {
            Border = BoxBorder.None,
            Padding = new Padding(2, 1, 2, 1),
        });

        AnsiConsole.Write(new Panel(new Rows(
            new Rule(ThisAssembly.Strings.FirstRun.PrivacyTitle).RuleStyle(Color.MediumPurple2).LeftJustified(),
            new Markup(ThisAssembly.Strings.FirstRun.Privacy)))
        {
            Border = BoxBorder.None,
            Padding = new Padding(2, 1, 2, 1),
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
