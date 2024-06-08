﻿using System.ComponentModel;
using DotNetConfig;
using Spectre.Console;
using Spectre.Console.Cli;
using static Spectre.Console.AnsiConsole;
using static ThisAssembly;

namespace Devlooped.Sponsors;

[Description("Manages sponsorlink configuration")]
public class ConfigCommand(Config config) : Command<ConfigCommand.ConfigSettings>
{
    public class ConfigSettings : CommandSettings
    {
        [Description("Enable or disable automatic synchronization of expired manifests.")]
        [CommandOption("--autosync")]
        public bool? AutoSync { get; set; }

        [Description("Clear all cached credentials used for manifest synchronization.")]
        [CommandOption("--clear")]
        public bool? Clear { get; set; }

        /// <summary>
        /// Property used to modify the namespace from tests for scoping stored passwords.
        /// </summary>
        [CommandOption("--namespace", IsHidden = true)]
        public string Namespace { get; set; } = "com.devlooped";
    }

    public override int Execute(CommandContext context, ConfigSettings settings)
    {
        if (settings.AutoSync != null)
        {
            // We persist as a string since that makes it easier to parse from MSBuild.
            config.SetString("sponsorlink", "autosync", settings.AutoSync.Value.ToString().ToLowerInvariant());
            if (settings.AutoSync == true)
                MarkupLine(Strings.Sync.AutoSyncEnabled);
            else
                MarkupLine(Strings.Sync.AutoSyncDisabled);
        }

        if (settings.Clear == true)
        {
            // Check existing creds, if any
            var store = GitCredentialManager.CredentialManager.Create(settings.Namespace);
            var accounts = store.GetAccounts("https://github.com");
            foreach (var clientId in accounts)
                store.Remove("https://github.com", clientId);

            MarkupLine(Strings.Config.Clear(accounts.Count));
            foreach (var clientId in accounts)
                Write(new Padder(new Markup(Strings.Config.ClearClientId(clientId)), new Padding(3, 0, 0, 0)));
        }

        return 0;
    }
}
