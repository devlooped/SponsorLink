using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.CodeAnalysis;

namespace Devlooped;

/// <summary>
/// Gathers and exposes sponsor link attribution for a given sponsorable 
/// account that consumes SponsorLink. 
/// </summary>
/// <remarks>
/// Intended usage is for the library author to add roslyn analyzer/source 
/// generator to the package, which implements <see cref="IIncrementalGenerator"/> 
/// and instantiates <see cref="SponsorLink"/> with the sponsorable account 
/// in its constructor. In the <see cref="IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext)"/>, 
/// the library generator should invoke <see cref="Initialize(IncrementalGeneratorInitializationContext)"/> 
/// in turn.
/// </remarks>
public class SponsorLink
{
    static readonly HttpClient http = new();
    static readonly Random rnd = new();

    string sponsorable;

    /// <summary>
    /// Creates the sponsor link instance for the given sponsorable account, used to 
    /// check for active installation and sponsorships for the current user (given 
    /// their configured git email).
    /// </summary>
    /// <param name="sponsorable">A sponsorable account that has been properly provisioned with SponsorLink.</param>
    public SponsorLink(string sponsorable) => this.sponsorable = sponsorable;

    /// <summary>
    /// Initializes the sponsor link checks during builds.
    /// </summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var dirs = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(x =>
                x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.SourceItemType", out var itemType) &&
                itemType == "MSBuildProject")
            .Select((x, c) =>
            {
                var opt = x.Right.GlobalOptions;
                var insideEditor =
                    !opt.TryGetValue("build_property.BuildingInsideVisualStudio", out var value) ||
                    !bool.TryParse(value, out var bv) ? null : (bool?)bv;

                // Override value if we detect R#/Rider in use.
                if (Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(k => k.StartsWith("RESHARPER")))
                    insideEditor = true;

                var dtb =
                    !opt.TryGetValue("build_property.DesignTimeBuild", out value) ||
                    !bool.TryParse(value, out bv) ? null : (bool?)bv;

                return new State(Path.GetDirectoryName(x.Left.Path), insideEditor, dtb);
            });

        context.RegisterSourceOutput(dirs.Collect(), CheckSponsor);

    }

    void CheckSponsor(SourceProductionContext spc, ImmutableArray<State> states)
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("DEBUG_SPONSORLINK"), out var debug) && debug)
            Debugger.Launch();

        if (states.IsDefaultOrEmpty || states[0].InsideEditor == null)
        {
            // Broken state
            spc.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("SL01", "Invalid SponsorLink configuration", "Invalid SponsorLink configuration", "SponsorLink", DiagnosticSeverity.Error, true, helpLinkUri: "https://devlooped.com/sponsorlink/SL01.html"),
                Location.None));
            return;
        }

        // We never pause in DTB
        if (states[0].DesignTimeBuild == true)
        {
            // report warning diagnostic that we don't do anything in design-time builds
            spc.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("SL02", "SponsorLink is disabled in design-time builds", "SponsorLink is disabled in design-time builds", "SponsorLink", DiagnosticSeverity.Warning, true, helpLinkUri: "https://devlooped.com/sponsorlink/SL02.html"),
                Location.None));
            return;
        }

        // We never pause in non-IDE builds
        if (states[0].InsideEditor == false)
        {
            // report warning diagnostic that we don't do anything in CLI builds
            spc.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor("SL03", "SponsorLink is disabled in CLI builds", "SponsorLink is disabled in CLI builds", "SponsorLink", DiagnosticSeverity.Warning, true, helpLinkUri: "https://devlooped.com/sponsorlink/SL03.html"),
                Location.None));
            return;
        }

        // If there is no network at all, don't do anything.
        if (!NetworkInterface.GetIsNetworkAvailable())
            return;

        try
        {
            var proc = Process.Start(new ProcessStartInfo("git", "config --get user.email")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = states[0].Path
            });
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                var email = proc.StandardOutput.ReadToEnd().Trim();
                if (string.IsNullOrEmpty(email))
                {
                    // telemetry? has git but no email?
                    return;
                }

                // Check app install
                var installed = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/apps/{email}", spc.CancellationToken);
                if (installed == null)
                {
                    // telemetry? error checking for url?
                    return;
                }

                if (installed == false)
                {
                    // report warning diagnostic that sponsorlink needs to be installed
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("SL04", "SponsorLink is not installed", "SponsorLink is not installed", "SponsorLink", DiagnosticSeverity.Warning, true, helpLinkUri: "https://devlooped.com/sponsorlink/SL04.html"),
                        Location.None));
                    return;
                }

                // Check sponsorship
                var sponsoring = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/devlooped/{email}", spc.CancellationToken);
                if (installed == null)
                {
                    // telemetry? error checking for url?
                    return;
                }

                if (sponsoring == false)
                {
                    // report warning diagnostic with the email we got as text
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("SL05", "SponsorLink found {0} is not sponsoring 😢", "SponsorLink found {0} is not sponsoring", "SponsorLink", DiagnosticSeverity.Warning, true, helpLinkUri: "https://devlooped.com/sponsorlink/SL05.html"),
                        Location.None, email));

                    Thread.Sleep(rnd.Next(1000, 3000));
                }
                else
                {
                    // report warning diagnostic with the email we got as text
                    spc.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("SL06", "SponsorLink found {0} is sponsoring! 💟", "SponsorLink found {0} is sponsoring! 💟", "SponsorLink", DiagnosticSeverity.Warning, true, helpLinkUri: "https://devlooped.com/sponsorlink/SL06.html"),
                        Location.None, email));
                }

                spc.AddSource("SponsorLink.g", $"// {email}");
            }
            else
            {
                // telemetry? no git?
            }
        }
        catch
        {
            // telemetry?
        }
    }

    bool? UrlExists(string url, CancellationToken cancellation)
    {
        var ev = new ManualResetEventSlim();
        bool? exists = null;
        http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), cancellation)
            .ContinueWith(t =>
            {
                if (!t.IsFaulted)
                    exists = t.IsCompleted && t.Result.IsSuccessStatusCode;

                ev.Set();
            });

        ev.Wait(cancellation);
        return exists;
    }

    class State
    {
        public State(string path, bool? insideEditor, bool? designTimeBuild)
        {
            Path = path;
            InsideEditor = insideEditor;
            DesignTimeBuild = designTimeBuild;
        }

        public string Path { get; }
        public bool? InsideEditor { get; }
        public bool? DesignTimeBuild { get; }
    }
}
