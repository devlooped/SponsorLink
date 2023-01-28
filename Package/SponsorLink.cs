using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped;

/// <summary>
/// Provides build-time checks for sponsorships. Derived classes must 
/// annotate derived classes with both <see cref="DiagnosticAnalyzerAttribute"/> 
/// as well as <see cref="GeneratorAttribute"/> in order for SponsorLink to 
/// function properly.
/// </summary>
public abstract class SponsorLink : DiagnosticAnalyzer, IIncrementalGenerator
{
    static readonly HttpClient http = new();
    static readonly Random rnd = new();

    readonly string sponsorable;
    readonly string product;
    readonly SponsorLinkSettings settings;
    readonly ImmutableArray<DiagnosticDescriptor> diagnostics = ImmutableArray<DiagnosticDescriptor>.Empty;

    /// <summary>
    /// Manages the sharing and reporting of diagnostics across the source generator 
    /// and the diagnostic analyzer, to avoid doing the online check more than once.
    /// </summary>
    internal static DiagnosticsManager Diagnostics { get; } = new();

    /// <summary>
    /// Initializes SponsorLink with the given sponsor account and product name, for fully 
    /// customized diagnostics. You must override <see cref="SupportedDiagnostics"/> and 
    /// <see cref="OnDiagnostic(string, DiagnosticKind)"/> for this to work.
    /// </summary>
    /// <param name="sponsorable">The sponsor account that users should sponsor.</param>
    /// <param name="product">The name of product that is using sponsor link.</param>
    /// <remarks>
    /// This constructor overload allows full customization of reported diagnostics. The 
    /// base class won't report anything in this case and just expose the support 
    /// diagnostics from <see cref="SupportedDiagnostics"/> and invoke 
    /// <see cref="OnDiagnostic(string, DiagnosticKind)"/> for the various supported 
    /// sponsoring scenarios.
    /// </remarks>
    protected SponsorLink(string sponsorable, string product)
        : this(SponsorLinkSettings.Create(sponsorable, product)) { }

    /// <summary>
    /// Initializes the analyzer with the default behavior configured with the 
    /// given settings. The specific diagnostics supported and reported are 
    /// managed by the base class and require no additional overrides or 
    /// customizations.
    /// </summary>
    /// <param name="settings">The settings for the analyzer and generator.</param>
    protected SponsorLink(SponsorLinkSettings settings)
    {
        sponsorable = settings.Sponsorable;
        product = settings.Product;
        diagnostics = settings.SupportedDiagnostics;
        this.settings = settings;
    }

    /// <summary>
    /// Exposes the supported diagnostics.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => diagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationAction(ReportDiagnostic);
    }

    /// <summary>
    /// Initializes the sponsor link checks during builds.
    /// </summary>
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analyzerFile = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(x =>
                x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.SourceItemType", out var itemType) &&
                itemType == "Analyzer" &&
                x.Right.GetOptions(x.Left).TryGetValue("build_metadata.Analyzer.NuGetPackageId", out _))
            .Where(x => File.Exists(x.Left.Path))
            .Select((x, c) => new
            {
                x.Left.Path,
                PackageId = 
                    x.Right.GetOptions(x.Left).TryGetValue("build_metadata.Analyzer.NuGetPackageId", out var packageId) ?
                    packageId : ""
            })
            .Collect();

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
                if (Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(k =>
                        k.StartsWith("RESHARPER") ||
                        k.StartsWith("IDEA_")))
                    insideEditor = true;

                var dtb =
                    !opt.TryGetValue("build_property.DesignTimeBuild", out value) ||
                    !bool.TryParse(value, out bv) ? null : (bool?)bv;

                // SponsorLink authors can debug it by setting up a IsRoslynComponent=true project, 
                // but also need to set this property in the project, since the debugger will set DesignTimeBuild=true.
                if (opt.TryGetValue("build_property.DebugSponsorLink", out value) && 
                    bool.TryParse(value, out var debugSL) && debugSL)
                    // Reset value to what it is in CLI builds
                    dtb = null;

                return new BuildInfo(x.Left.Path, insideEditor, dtb);
            })
            .Combine(context.CompilationProvider)
            // Add compilation options to check for warning disable.
            .Select((x, c) => x.Left.WithCompilationOptions(x.Right.Options))
            // Add our analyzer file path to check for installation time
            .Combine(analyzerFile)
            .Select((x, c) =>
            {
                // Try to locate the right file and get its write time to detect install/restore time
                var path = x.Right.FirstOrDefault(f => f.PackageId == settings.PackageId)?.Path;
                if (!string.IsNullOrEmpty(path))
                    return x.Left.WithInstallTime(File.GetLastWriteTime(path));

                // We won't set an install time, and therefore we'll just start doing pauses 
                // righ-away with the max configured pause. This will happen for example if 
                // the settings don't provide a package id, or it differs from the product name.
                return x.Left;
            });

        context.RegisterSourceOutput(dirs.Collect(), CheckSponsor);
    }

    /// <summary>
    /// Performs an action when the given diagnostic <paramref name="kind"/> is verified for 
    /// the current user. If an actual diagnostic should be reported, a non-null value can be 
    /// returned.
    /// </summary>
    /// <remarks>
    /// Returns null if no diagnostics should be reported for the given diagnostic <paramref name="kind"/>.
    /// </remarks>
    protected virtual Diagnostic? OnDiagnostic(string projectPath, DiagnosticKind kind)
    {
        var descriptor = SupportedDiagnostics.FirstOrDefault(x => x.CustomTags.Contains(kind.ToString()));
        if (descriptor == null)
            return null;

        switch (kind)
        {
            case DiagnosticKind.AppNotInstalled:
            case DiagnosticKind.UserNotSponsoring:
                // Add a random configurable pause in this case.
                var (pause, suffix) = GetPause();
                var diag = Diagnostic.Create(descriptor, null, product, sponsorable, suffix);

                WriteMessage(sponsorable, product, Path.GetDirectoryName(projectPath), diag);

                if (pause > 0)
                    Thread.Sleep(pause);

                return diag;
            case DiagnosticKind.Thanks:
                return WriteMessage(sponsorable, product, Path.GetDirectoryName(projectPath),
                    Diagnostic.Create(descriptor, null, product, sponsorable));
            default:
                return default;
        }
    }

    void CheckSponsor(SourceProductionContext context, ImmutableArray<BuildInfo> states)
    {
        if (states.IsDefaultOrEmpty || states[0].InsideEditor == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticsManager.Broken, null));
            return;
        }

        var state = states[0];
        // Updates the InstallTime setting the first time.
        if (state.InstallTime != null && settings.InstallTime == null)
            settings.InstallTime = state.InstallTime;

        // We never pause in DTB
        if (state.DesignTimeBuild == true)
            return;

        // We never pause in non-IDE builds
        if (state.InsideEditor == false)
            return;

        // If there is no network at all, don't do anything.
        if (!NetworkInterface.GetIsNetworkAvailable())
            return;

        var email = GetEmail(Path.GetDirectoryName(state.ProjectPath));
        // No email configured in git. Weird.
        if (string.IsNullOrEmpty(email))
            return;

        // Check app install and sponsoring status
        var installed = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/apps/{email}", context.CancellationToken);
        var sponsoring = UrlExists($"https://devlooped.blob.core.windows.net/sponsorlink/{sponsorable}/{email}", context.CancellationToken);

        // Faulted HTTP HEAD request checking for url?
        if (installed == null || sponsoring == null)
            return;

        var kind =
            installed == false ? DiagnosticKind.AppNotInstalled :
            sponsoring == false ? DiagnosticKind.UserNotSponsoring :
            DiagnosticKind.Thanks;

        var diagnostic = OnDiagnostic(state.ProjectPath, kind);
        if (diagnostic != null)
            Diagnostics.Push(sponsorable, product, state.ProjectPath, diagnostic);
    }

    void ReportDiagnostic(CompilationAnalysisContext context)
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("DEBUG_SPONSORLINK"), out var debug) && debug &&
            !Debugger.IsAttached)
            Debugger.Launch();

        var opt = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (!opt.TryGetValue("build_property.MSBuildProjectFullPath", out var projectPath))
            return;
        
        // If we can get the generator-pushed diagnostic in the same process, we 
        // report it here and exit. Analyzer-reported diagnostics have proper help links.
        var diagnostic = Diagnostics.Pop(sponsorable, product, projectPath);
        if (diagnostic != null)
        {
            Diagnostics.ReportDiagnosticOnce(context, diagnostic, sponsorable, product);
            return;
        }

        // If this particular build did not generate a new diagnostic (i.e. it was an 
        // incremental build where the project file didn't change at all, we still need 
        // to report the diagnostic or it will go away immediately in a subsequent build.
        // This keeps the diagnostic "pinned" until an actual new check is performed, but 
        // makes sure we're not doing the online check anymore after the initial check.
        // This keeps DTBs quick and the editor super responsive and is effectively the 
        // communication "channel" between past runs of the generator check and the analyzer 
        // reporting live in VS.

        var productDir = Path.Combine(Path.GetDirectoryName(projectPath), "obj", "SponsorLink", sponsorable, product);
        if (!Directory.Exists(productDir))
            return;

        foreach (var file in Directory.EnumerateFiles(productDir, "*.txt"))
        {
            var parts = Path.GetFileName(file).Split('.');
            if (parts.Length < 2)
                continue;

            var id = parts[0];
            var severity = parts[1];
            var descriptor = SupportedDiagnostics.FirstOrDefault(x => x.Id == id);
            if (descriptor == null)
                continue;

            var text = File.ReadAllText(file).Trim();
            // NOTE: we recreate the descriptor here, since that's cheaper than attempting 
            // to recreate the format string that produced the given message in the file. 
            diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
                id: descriptor.Id,
                title: descriptor.Title,
                messageFormat: text,
                category: descriptor.Category,
                defaultSeverity: descriptor.DefaultSeverity,
                isEnabledByDefault: descriptor.IsEnabledByDefault,
                description: descriptor.Description,
                helpLinkUri: descriptor.HelpLinkUri,
                customTags: descriptor.CustomTags.ToArray()),
                // If we provide a non-null location, the entry for some reason is no longer shown in VS :/
                null);

            Diagnostics.ReportDiagnosticOnce(context, diagnostic, sponsorable, product);
        }
    }

    (int pause, string suffix) GetPause()
    {
        if (settings == null)
            return (0, "");

        if (settings.InstallTime == null)
            return GetPaused(rnd.Next(settings.PauseMin, settings.PauseMax));

        var daysOld = (int)DateTime.Now.Subtract(settings.InstallTime.Value).TotalDays;

        // Never pause the first day of the install. Just warnings.
        if (daysOld == 0)
            return (0, string.Empty);

        // Turn days into milliseconds, used for the pause.
        var daysPause = daysOld * 1000;

        // From second day, the max pause will increase from days old until the max pause.
        return GetPaused(rnd.Next(settings.PauseMin, Math.Min(daysPause, settings.PauseMax)));
    }

    static (int pause, string suffix) GetPaused(int pause) => (pause, ThisAssembly.Strings.BuildPaused(pause));

    static Diagnostic WriteMessage(string sponsorable, string product, string projectDir, Diagnostic diag)
    {
        var objDir = Path.Combine(projectDir, "obj", "SponsorLink", sponsorable, product);
        if (Directory.Exists(objDir))
        {
            foreach (var file in Directory.EnumerateFiles(objDir))
                File.Delete(file);
        }

        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, $"{diag.Id}.{diag.Severity}.txt"), diag.GetMessage());
        
        return diag;
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

    /// <summary>
    /// Provides information about the build that was checked for sponsor linking.
    /// </summary>
    class BuildInfo
    {
        internal BuildInfo(string path, bool? insideEditor, bool? designTimeBuild)
        {
            ProjectPath = path;
            InsideEditor = insideEditor;
            DesignTimeBuild = designTimeBuild;
        }

        /// <summary>
        /// The full path of the project being built.
        /// </summary>
        public string ProjectPath { get; }
        /// <summary>
        /// Whether the build is happening inside an editor.
        /// </summary>
        public bool? InsideEditor { get; }
        /// <summary>
        /// Whether the build is a design-time build.
        /// </summary>
        public bool? DesignTimeBuild { get; }
        /// <summary>
        /// Compilation options being used for the build.
        /// </summary>
        public CompilationOptions? CompilationOptions { get; private set; }

        /// <summary>
        /// The installation/restore time of SponsorLink.
        /// </summary>
        public DateTime? InstallTime { get; private set; }

        internal BuildInfo WithCompilationOptions(CompilationOptions options)
        {
            CompilationOptions = options;
            return this;
        }

        internal BuildInfo WithInstallTime(DateTime? installTime)
        {
            InstallTime = installTime;
            return this;
        }
    }
}
