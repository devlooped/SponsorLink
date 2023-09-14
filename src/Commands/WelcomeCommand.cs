using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Executes the first-run experience")]
public class WelcomeCommand(ICommandApp app, Account user) : Command
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

        //var choices = new[] { ThisAssembly.Strings.FirstRun.Accept, ThisAssembly.Strings.FirstRun.Cancel };
        //var selection = AnsiConsole.Prompt(new SelectionPrompt<string>()
        //        .Title(ThisAssembly.Strings.FirstRun.Acceptance)
        //        .AddChoices(choices));

        //if (selection == choices[1])
        //{
        //    return -1;
        //}

        if (AnsiConsole.Confirm(ThisAssembly.Strings.FirstRun.SyncNow))
        {
            return app.Run(new[] { "sync" });
        }

        //var choices = new[] { ThisAssembly.Strings.Yes, ThisAssembly.Strings.No };
        //var selection = AnsiConsole.Prompt(new SelectionPrompt<string>()
        //        .Title(ThisAssembly.Strings.FirstRun.SyncNow)
        //        .AddChoices(choices));

        //if (selection == choices[0])
        //{
        //    return app.Run(new[] { "sync" });
        //}

        return 0;
    }
}
