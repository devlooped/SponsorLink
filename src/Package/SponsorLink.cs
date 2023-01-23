using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Devlooped;

/// <summary>
/// Gathers and exposes sponsor link attribution for a given sponsorable 
/// account that consumes SponsorLink. 
/// </summary>
/// <remarks>
/// Intended usage is for the library author to add a Roslyn source 
/// generator to the package, and initialize a new instance of this class 
/// with both the sponsorable account and the product name to use for the checks.
/// </remarks>
public class SponsorLink
{
    static readonly HttpClient http = new();
    static readonly Random rnd = new();
    static readonly string[] defaultTags = new[] { "DoesNotSupportF1Help" };

    readonly string sponsorable;
    readonly Action<SourceProductionContext, string> notInstalled;
    readonly Action<SourceProductionContext, string> nonSponsor;
    readonly Action<SourceProductionContext, string> activeSponsor;

    /// <summary>
    /// Creates the sponsor link instance for the given sponsorable account, used to 
    /// check for active installation and sponsorships for the current user (given 
    /// their configured git email).
    /// </summary>
    /// <param name="sponsorable">A sponsorable account that has been properly provisioned with SponsorLink.</param>
    /// <param name="product">The product developed by <paramref name="sponsorable"/> that is checking the sponsorship link.</param>
    public SponsorLink(string sponsorable, string product)
        : this(sponsorable, product, 2000, 4000) { }

    /// <summary>
    /// Creates the sponsor link instance for the given sponsorable account, used to 
    /// check for active installation and sponsorships for the current user (given 
    /// their configured git email).
    /// </summary>
    /// <param name="sponsorable">A sponsorable account that has been properly provisioned with SponsorLink.</param>
    /// <param name="product">The product developed by <paramref name="sponsorable"/> that is checking the sponsorship link.</param>
    /// <param name="pauseMin">Min random milliseconds to apply during build for non-sponsored users. Use 0 for no pause.</param>
    /// <param name="pauseMax">Max random milliseconds to apply during build for non-sponsored users. Use 0 for no pause.</param>
    public SponsorLink(string sponsorable, string product, int pauseMin, int pauseMax)
        : this(sponsorable,
              (context, path) =>
              {
                  context.ReportDiagnostic(Diagnostic.Create(
                      "SL02", "SponsorLink",
                      $"{product} uses SponsorLink to properly attribute your sponsorship with {sponsorable}. Please install the GitHub app at https://github.com/apps/sponsorlink.",
                      DiagnosticSeverity.Warning, DiagnosticSeverity.Warning,
                      true, 1, false,
                      $"{product} uses SponsorLink to properly attribute your sponsorship with {sponsorable}.",
                      $"Please install the GitHub app at https://github.com/apps/sponsorlink.",
                      helpLink: "https://github.com/apps/sponsorlink",
                      location: Location.Create(path, new TextSpan(0, 0), new LinePositionSpan()),
                      customTags: defaultTags));
                  // Add a random configurable pause in this case.
                  Thread.Sleep(rnd.Next(pauseMin, pauseMax));
              },
              (context, path) =>
              {
                  context.ReportDiagnostic(Diagnostic.Create(
                      "SL03", "SponsorLink",
                      $"Please consider supporting {product} development at https://github.com/sponsors/{sponsorable}.",
                      DiagnosticSeverity.Warning, DiagnosticSeverity.Warning,
                      true, 1, false,
                      $"Please consider supporting {product} development at https://github.com/sponsors/{sponsorable}.",
                      $"SponsorLink allows {product} to attribute your sponsorship with {sponsorable}.",
                      helpLink: $"https://github.com/sponsors/{sponsorable}",
                      location: Location.Create(path, new TextSpan(0, 0), new LinePositionSpan()),
                      customTags: defaultTags));
                  // Add a random configurable pause in this case.
                  Thread.Sleep(rnd.Next(pauseMin, pauseMax));

              },
              (context, path) =>
              {
                  context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                      "SL04",
                      "SponsorLink",
                      "Thank you for supporting {0} development with your sponsorship of {1} 💟!",
                      "SponsorLink",
                      DiagnosticSeverity.Info,
                      true,
                      $"{product} uses SponsorLink to attribute your support via GitHub Sponsors.",
                      $"https://github.com/sponsors/{sponsorable}",
                      defaultTags),
                      null,
                      //location: location,
                      product, sponsorable));
              })
    { }

    /// <summary>
    /// Advanced overload that allows granular behavior customization for the sponsorable account.
    /// </summary>
    /// <param name="sponsorable">A sponsorable account that has been properly provisioned with SponsorLink.</param>
    /// <param name="notInstalled">Action to invoke when the user has not installed the SponsorLink app yet (or has disabled it).</param>
    /// <param name="nonSponsor">Action to invoke when the user has installed the app but is not sponsoring.</param>
    /// <param name="activeSponsor">Action to invoke when the user has installed the app and is sponsoring the account.</param>
    /// <remarks>
    /// The action delegates receive the generator context and the current project path.
    /// </remarks>
    public SponsorLink(string sponsorable, 
        Action<SourceProductionContext, string> notInstalled, 
        Action<SourceProductionContext, string> nonSponsor, 
        Action<SourceProductionContext, string> activeSponsor)
    {
        this.sponsorable = sponsorable;
        this.notInstalled = notInstalled;
        this.nonSponsor = nonSponsor;
        this.activeSponsor = activeSponsor;
    }

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

                return new State(x.Left.Path, insideEditor, dtb);
            });

        context.RegisterSourceOutput(dirs.Collect(), CheckSponsor);

    }

    void CheckSponsor(SourceProductionContext context, ImmutableArray<State> states)
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("DEBUG_SPONSORLINK"), out var debug) && debug)
            if (Debugger.IsAttached)
                Debugger.Break();
            else
                Debugger.Launch();

        if (states.IsDefaultOrEmpty || states[0].InsideEditor == null)
        {
            // Broken state
            context.ReportDiagnostic(Diagnostic.Create(
                "SL01", "SponsorLink",
                "Invalid SponsorLink configuration",
                DiagnosticSeverity.Error, DiagnosticSeverity.Error,
                true, 0, false,
                "Invalid SponsorLink configuration",
                "SponsorLink has been incorrectly configured. See https://devlooped.com/sponsorlink/SL01.html.",
                location: Location.Create(states[0].Path, new TextSpan(0, 0), new LinePositionSpan()),
                helpLink: "https://devlooped.com/sponsorlink/SL02.html",
                customTags: defaultTags));

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

        var email = GetEmail(Path.GetDirectoryName(states[0].Path));
        // No email configured in git. Weird.
        if (string.IsNullOrEmpty(email))
            return;

        // Check app install and sponsoring status
        var installed = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/apps/{email}", context.CancellationToken);
        var sponsoring = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/devlooped/{email}", context.CancellationToken);

        // Faulted HTTP HEAD request checking for url?
        if (installed == null || sponsoring == null)
            return;

        if (installed == false)
            notInstalled(context, states[0].Path);
        else if (sponsoring == false)
            nonSponsor(context, states[0].Path);
        else
            activeSponsor(context, states[0].Path);
    }

    static string? GetEmail(string workingDirectory)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo("git", "config --get user.email")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            });
            proc.WaitForExit();

            // Couldn't run git config, so we can't check for sponsorship, no email to check.
            if (proc.ExitCode != 0)
                return null;

            return proc.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            // Git not even installed.
        }

        return null;
    }

    static bool? UrlExists(string url, CancellationToken cancellation)
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

    /*
    /// <summary>
    /// Gets the supported diagnostics.
    /// </summary>
    public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

    /// <summary>
    /// Initializes the sponsor link checks during builds.
    /// </summary>
    public void InitializeAnalyzer(AnalysisContext context)
    {
        // Just in case the caller doesn't do this (recommended) action already.
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationAction(ThankSponsor);
    }

    void ThankSponsor(CompilationAnalysisContext context)
    {
        var opt = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        var insideEditor =
            !opt.TryGetValue("build_property.BuildingInsideVisualStudio", out var value) ||
            !bool.TryParse(value, out var bv) ? null : (bool?)bv;

        // Override value if we detect R#/Rider in use.
        if (Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(k => k.StartsWith("RESHARPER")))
            insideEditor = true;

        var dtb =
            !opt.TryGetValue("build_property.DesignTimeBuild", out value) ||
            !bool.TryParse(value, out bv) ? null : (bool?)bv;

        if (!opt.TryGetValue("build_property.MSBuildProjectFullPath", out var path))
            return;

        // Make DTB, CLI and offline builds quick. No need to thank in this case.
        if (dtb == true || insideEditor == false ||
            !NetworkInterface.GetIsNetworkAvailable())
            return;

        var email = GetEmail(Path.GetDirectoryName(path));
        if (string.IsNullOrEmpty(email))
        {
            // No email configured in git. Weird.
            return;
        }

        // Check app install and sponsoring status
        var installed = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/apps/{email}", context.CancellationToken);
        var sponsoring = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/devlooped/{email}", context.CancellationToken);

        if (installed == true && sponsoring == true)
            context.ReportDiagnostic(Diagnostic.Create(thanksDescriptor, Location.Create(path, 
                new TextSpan(0, 0), new LinePositionSpan()), product, sponsorable));
    }
    */
}
