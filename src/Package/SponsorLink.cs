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
    string product;

    /// <summary>
    /// Creates the sponsor link instance for the given sponsorable account, used to 
    /// check for active installation and sponsorships for the current user (given 
    /// their configured git email).
    /// </summary>
    /// <param name="sponsorable">A sponsorable account that has been properly provisioned with SponsorLink.</param>
    /// <param name="product">The product developed by <paramref name="sponsorable"/> that is checking the sponsorship link.</param>
    public SponsorLink(string sponsorable, string product)
        => (this.sponsorable, this.product)
        = (sponsorable, product);

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
                "SL01", "SponsorLink",
                "Invalid SponsorLink configuration",
                DiagnosticSeverity.Error, DiagnosticSeverity.Error,
                true, 0, false,
                "Invalid SponsorLink configuration",
                "SponsorLink has been incorrectly configured.",
                "https://devlooped.com/sponsorlink/SL01.html"));

            return;
        }

        // We never pause in DTB
        if (states[0].DesignTimeBuild == true)
            return;

        // We never pause in non-IDE builds
        if (states[0].InsideEditor == false)
            return;

        // If there is no network at all, don't do anything.
        if (!NetworkInterface.GetIsNetworkAvailable())
            return;

        string? email = null;

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

            // Couldn't run git config, so we can't check for sponsorship, no email to check.
            if (proc.ExitCode != 0)
                return;

            email = proc.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            // Git not even installed.
        }

        if (string.IsNullOrEmpty(email))
        {
            // No email configured in git. Weird.
            return;
        }

        // Check app install and sponsoring status
        var installed = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/apps/{email}", spc.CancellationToken);
        var sponsoring = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/devlooped/{email}", spc.CancellationToken);

        if (installed == null || sponsoring == null)
        {
            // Faulted HTTP HEAD request checking for url?
            return;
        }

        if (installed == false)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                "SL02", "SponsorLink",
                $"{product} uses SponsorLink to properly attribute your sponsorship with {sponsorable}. Please install the GitHub app.",
                DiagnosticSeverity.Warning, DiagnosticSeverity.Warning,
                true, 1, false,
                $"{product} uses SponsorLink to properly attribute your sponsorship with {sponsorable}. Please install the GitHub app.",
                helpLink: "https://github.com/apps/sponsorlink"));

            Thread.Sleep(rnd.Next(1000, 3000));
        }
        else if (sponsoring == false)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                "SL02", "SponsorLink",
                $"Please consider supporting {product} development by sponsoring {sponsorable} on GitHub.",
                DiagnosticSeverity.Warning, DiagnosticSeverity.Warning,
                true, 1, false,
                $"Please consider supporting {product} development by sponsoring {sponsorable} on GitHub.",
                helpLink: $"https://github.com/sponsors/{sponsorable}"));

            Thread.Sleep(rnd.Next(1000, 3000));
        }
        else
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                "SL02", "SponsorLink",
                $"Thank you for supporting {product} development with your sponsorship of {sponsorable} 💟!",
                DiagnosticSeverity.Info, DiagnosticSeverity.Info,
                true, 2, false,
                $"Thank you for supporting {product} development with your sponsorship of {sponsorable} 💟!",
                helpLink: $"https://github.com/sponsors/{sponsorable}"));
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
