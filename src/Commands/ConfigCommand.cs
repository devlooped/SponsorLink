using System.ComponentModel;
using DotNetConfig;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly.Strings;

namespace Devlooped.Sponsors;

[Description("Manages sponsorlink configuration")]
public class ConfigCommand(Config config) : Command<ConfigCommand.ConfigSettings>
{
    public class ConfigSettings : CommandSettings
    {
        [Description("Enable or disable automatic synchronization of expired manifests.")]
        [CommandOption("--autosync")]
        public bool? AutoSync { get; set; }
    }

    public override int Execute(CommandContext context, ConfigSettings settings)
    {
        if (settings.AutoSync != null)
        {
            // We persist as a string since that makes it easier to parse from MSBuild.
            config.SetString("sponsorlink", "autosync", settings.AutoSync.Value.ToString().ToLowerInvariant());
            if (settings.AutoSync == true)
                MarkupLine(Sync.AutoSyncEnabled);
            else
                MarkupLine(Sync.AutoSyncDisabled);
        }

        return 0;
    }
}
