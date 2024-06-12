using System.ComponentModel;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

public class ToSSettings : CommandSettings
{
    public const string ToSOption = "--tos";

    /// <summary>
    /// Allows explicit acceptance of the terms of service.
    /// </summary>
    [Description("Explicitly accept the [blue]terms of service[/] if they haven't been accepted already.")]
    [CommandOption(ToSOption, IsHidden = true)]
    public bool? ToS { get; set; }
}
