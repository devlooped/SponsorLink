using Spectre.Console;

namespace Devlooped;

partial class SponsorLink
{
    partial class Variables
    {
        /// <summary>
        /// Clears all environment variables used by SponsorLink.
        /// </summary>
        public static void Clear()
        {
            AccessToken = null;
            AnsiConsole.MarkupLine(ThisAssembly.Strings.Variables.Cleared(Constants.AccessTokenVariable));

            InstallationId = null;
            AnsiConsole.MarkupLine(ThisAssembly.Strings.Variables.Cleared(Constants.InstallationIdVariable));

            Manifest = null;
            AnsiConsole.MarkupLine(ThisAssembly.Strings.Variables.Cleared(Constants.ManifestVariable));
        }
    }
}