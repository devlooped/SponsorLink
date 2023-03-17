using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static Devlooped.Tracing;

namespace Devlooped;

/// <summary>
/// Provides build-time checks for sponsorships. Derived classes must 
/// annotate derived classes with both <see cref="DiagnosticAnalyzerAttribute"/> 
/// as well as <see cref="GeneratorAttribute"/> in order for SponsorLink to 
/// function properly.
/// </summary>
public abstract class SponsorLink : DiagnosticAnalyzer
    // For backwards compatibility only.
    , IIncrementalGenerator
{
    // We use a smaller timeout since analyzer/generator will run more frequently 
    // so we have more than one chance to get the right status, eventually. 
    // This is 1/4 of the HttpClientFactory default timeout, used for direct API calls.
#if DEBUG
    // Debug builds are slower, to give it a full second of timeout
    static TimeSpan NetworkTimeout => Debugger.IsAttached ?
        TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(1);
#else
    static TimeSpan NetworkTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
#endif

    static HttpClient http;

    static readonly Random rnd = new();
    static int quietDays = 15;
    static bool reportBroken = true;

    readonly string sponsorable;
    readonly string product;
    readonly SponsorLinkSettings settings;
    readonly ImmutableArray<DiagnosticDescriptor> diagnostics = ImmutableArray<DiagnosticDescriptor>.Empty;

    static SponsorLink()
    {
        Trace($"SponsorLink::cctor@{AppDomain.CurrentDomain.FriendlyName}");
        //Trace(string.Join(Environment.NewLine, Environment
        //    .GetEnvironmentVariables()
        //    .Cast<DictionaryEntry>()
        //    .OrderBy(x => x.Key)
        //    .Where(x => !"Path".Equals(x.Key))
        //    .Select(x => $"{x.Key}={x.Value}")));
        
        AppDomain.CurrentDomain.ProcessExit += (sender, args) => Trace("ProcessExit");
            
        http = HttpClientFactory.Create(NetworkTimeout);

        // Reads settings from storage, best-effort
        http.GetStringAsync("https://cdn.devlooped.com/sponsorlink/settings.ini")
            .ContinueWith(t =>
            {
                var values = t.Result
                    .Split(new[] { "\r\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => x[0] != '#')
                    .Select(x => x.Split(new[] { '=' }, 2))
                    .ToDictionary(x => x[0].Trim(), x => x[1].Trim());

                if (values.TryGetValue("quiet", out var value) && int.TryParse(value, out var days))
                    quietDays = days;

                if (values.TryGetValue("report-broken", out value) && bool.TryParse(value, out var report))
                    reportBroken = report;

#if RELEASE
                if (values.TryGetValue("network-timeout", out value) && double.TryParse(value, out var timeout))
                    NetworkTimeout = TimeSpan.FromMilliseconds(timeout);
#endif

            }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

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
        
        // Add the built-in ones to the dynamic diagnostics.
        diagnostics = settings.SupportedDiagnostics
            .Add(DiagnosticsManager.MissingProject)
            .Add(DiagnosticsManager.MissingDesignTimeBuild);
        
        this.settings = settings;
    }

    /// <summary>
    /// Exposes the supported diagnostics.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => diagnostics;

    /// <inheritdoc/>
#pragma warning disable RS1026 // Enable concurrent execution: we only enable it on RELEASE builds
    public override void Initialize(AnalysisContext context)
#pragma warning restore RS1026 // Enable concurrent execution
    {
#if RELEASE
        context.EnableConcurrentExecution();
#endif
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationAction(AnalyzeSponsors);
    }

    // NOTE: for backwards compatiblity only. We had both an analyzer and source generator before.
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context) { }


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
                var (warn, pause, suffix) = GetPause();
                if (!warn)
                    return null;

                // The Pause property is used by our default implementation to introduce the pause 
                // in an incremental-aware way.
                var diag = Diagnostic.Create(descriptor, null,
                    properties: new Dictionary<string, string?>
                    {
                        { "Pause", pause.ToString() },
                    }.ToImmutableDictionary(),
                    product, sponsorable, suffix);

                return WriteMessage(sponsorable, product, Path.GetDirectoryName(projectPath), diag);
            case DiagnosticKind.Thanks:
                return WriteMessage(sponsorable, product, Path.GetDirectoryName(projectPath),
                    Diagnostic.Create(descriptor, null, product, sponsorable));
            default:
                return default;
        }
    }

    void AnalyzeSponsors(CompilationAnalysisContext context)
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("DEBUG_SPONSORLINK"), out var debug) && debug &&
            !Debugger.IsAttached)
        {
            Debugger.Launch();
            // Refresh HTTP client so we can have the increased timeout from an attached debugger session.
            http = HttpClientFactory.Create(NetworkTimeout);
        }

        // We never report from non-IDE builds, which *will* invoke analyzers 
        // and may end up improperly notifying of build pauses when none was 
        // incurred, actually.
        if (!SessionManager.IsEditor)
        {
            Trace(SessionManager.IsEditor == false);
            return;
        }

        var globalOptions = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        var projectFile = globalOptions.TryGetValue("build_property.MSBuildProjectFullPath", out var fullPath) ?
                    fullPath : null;

        if (string.IsNullOrWhiteSpace(projectFile) || !File.Exists(projectFile))
        {
            SponsorCheck.ReportBroken("MissingProjectFullPath", null, settings, http);
            if (reportBroken)
                context.ReportDiagnostic(WriteMessage(
                    sponsorable, product, Directory.GetCurrentDirectory(),
                    Diagnostic.Create(DiagnosticsManager.MissingProject, null)));

            return;
        }

        if (!globalOptions.TryGetValue("build_property.DesignTimeBuild", out var designTimeValue))
        {
            SponsorCheck.ReportBroken("MissingDesignTimeBuild", Path.GetDirectoryName(projectFile), settings, http);
            if (reportBroken)
                context.ReportDiagnostic(WriteMessage(
                    sponsorable, product, Path.GetDirectoryName(projectFile!),
                    Diagnostic.Create(DiagnosticsManager.MissingDesignTimeBuild, null)));

            return;
        }

        var dependency = context.Options.AdditionalFiles
            .Where(x => context.Options.AnalyzerConfigOptionsProvider.GetOptions(x) is var fileOptions &&
                fileOptions.TryGetValue("build_metadata.AdditionalFiles.SourceItemType", out var itemType) &&
                itemType == "PackageDependencies" &&
                fileOptions.TryGetValue("build_metadata.AdditionalFiles.SourceIdentity", out var itemSpec) &&
                itemSpec == settings.PackageId)
            .Select(x => context.Options.AnalyzerConfigOptionsProvider.GetOptions(x).TryGetValue("build_metadata.AdditionalFiles.ParentPackage", out var parent) ?
                parent : null)
            .ToImmutableList();

        // We should not perform the check if the project is not referencing the package 
        // We detect this by checking the dependencies (see runtime targets which add 
        // PackageDependencies for the settings.PackageId from @SponsorablePackageId), 
        // and if the dependency is found and has a non-null ParentPackage, it means it's 
        // a transitive dependency.
        // Note that we default to being non-transitive.
        var shouldSkip = !settings.Transitive && dependency.Any(x => x != null);
        if (shouldSkip)
        {
            Trace("Skipping: transitively referenced.");
            return;
        }

        var designTimeBuild = !bool.TryParse(designTimeValue, out var bv) ? null : (bool?)bv;

        // SponsorLink authors can debug it by setting up a IsRoslynComponent=true project, 
        // but also need to set this property in the project, since the debugger will set DesignTimeBuild=true.
        if (globalOptions.TryGetValue("build_property.DebugSponsorLink", out var dsl) &&
            bool.TryParse(dsl, out var debugSL) && debugSL)
            // Reset value to what it is in CLI builds
            designTimeBuild = null;


        var info = new BuildInfo(projectFile!, designTimeBuild);

        // We MUST always re-report previously built diagnostics because 
        // otherwise they go away as soon as they are reported by a real 
        // build. The IDE re-runs the analyzers at various points and we 
        // want the real build diagnostics to remain visible since otherwise 
        // the user can miss what happened. Granted, a message saying the 
        // build was paused might be misleading since upon reopening VS 
        // with no build performed yet, a previously created diagnostic 
        // might be reported again, but that's a small price to pay.
        // So: DO NOT TRY TO AVOID REPORTING AGAIN ON DTB

        // If this particular build did not generate a new diagnostic (i.e. it was an 
        // incremental build where the project file didn't change at all, we still need 
        // to report the diagnostic or it will go away immediately in a subsequent build.
        // This keeps the diagnostic "pinned" until an actual new check is performed, but 
        // makes sure we're not doing the online check anymore after the initial check.
        // This keeps DTBs quick and the editor super responsive and is effectively the 
        // communication "channel" between past runs of the generator check and the analyzer 
        // reporting live in VS.

        // We never pause in DTB
        if (info.DesignTimeBuild == true)
        {
            Trace(nameof(ReportExisting), info.DesignTimeBuild == true);
            ReportExisting(context, projectFile);
            return;
        }

        CheckAndReport(context, info);
    }

    /// <summary>
    /// Runs a full check against URLs for sponsorship status. Only runs on full, editor builds.
    /// </summary>
    void CheckAndReport(CompilationAnalysisContext context, BuildInfo info)
    {
        if (settings.InstallTime != null)
        {
            info = info.WithInstallTime(settings.InstallTime);
        }
        else
        {
            // Attempt to calculate update time for quiet-days check
            var installTime = context.Options.AdditionalFiles
                .Where(x =>
                {
                    var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(x);
                    return options.TryGetValue("build_metadata.AdditionalFiles.SourceItemType", out var itemType) &&
                        itemType == "Analyzer" &&
                        // Filter analyzer items that actually have an originating NuGetPackageId metadata
                        options.TryGetValue("build_metadata.AdditionalFiles.NuGetPackageId", out var packageId);
                })
                // Adding this since we check for the file write time... Is this needed?
                .Where(x => File.Exists(x.Path))
                .Select((x, c) => new
                {
                    x.Path,
                    PackageId = context.Options.AnalyzerConfigOptionsProvider.GetOptions(x)
                        .TryGetValue("build_metadata.AdditionalFiles.NuGetPackageId", out var packageId) ?
                        packageId : ""
                })
                .Where(x => x.PackageId == settings.PackageId)
                .Select(x => (DateTime?)File.GetCreationTime(x.Path))
                .FirstOrDefault();

            info = info.WithInstallTime(installTime);

            // Updates the InstallTime setting the first time.
            if (settings.InstallTime == null)
                settings.InstallTime = info.InstallTime;
        }

        // NOTE: we run the check even if we may be up to date, since the status might have changed, 
        // going from NotSponsoring to Sponsoring, for example. We want to clear the warning ASAP in that case.

        var ev = new ManualResetEventSlim();
        SponsorStatus? status = default;
        SponsorCheck.CheckAsync(Path.GetDirectoryName(info.ProjectPath), settings.Sponsorable, settings.Product, settings.PackageId, settings.Version, http)
            .ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                    status = t.Result;
                else if (t.Status == TaskStatus.Faulted)
                    Trace(t.Exception.InnerException.ToString());

                ev.Set();
            });
        // Wait a bit more than the timeout to make sure we don't get a false cancellation.
        ev.Wait(NetworkTimeout.Add(TimeSpan.FromMilliseconds(100)), context.CancellationToken);

        if (status == null)
        {
            Trace("Could not get status");
            return;
        }

        var kind = status.Value switch
        {
            SponsorStatus.AppMissing => DiagnosticKind.AppNotInstalled,
            SponsorStatus.NotSponsoring => DiagnosticKind.UserNotSponsoring,
            _ => DiagnosticKind.Thanks,
        };

        // We check only once per-project, per-session as long as the diagnostic kind doesn't change.
        if (SessionManager.TryGet(info.ProjectPath, out var lastCheck) &&
            lastCheck == kind)
        {
            Trace($"Skipping: lastCheck == {kind}");
            ClearExisting(info.ProjectPath);
            return;
        }

        // If the given kind was already reported, no-op. For example, for ThisAssembly, each 
        // package will cause a different Diagnostic but for the same sponsorable+product combination, 
        // but we don't want to emit duplicates.
        if (Diagnostics.TryPeek(sponsorable, product, info.ProjectPath, out var diagnostic) &&
            diagnostic != null && diagnostic.Descriptor.IsKind(kind))
        {
            Trace($"Skipping: already reported {sponsorable}/{product} w/{kind}");
            // Trace("Clearing existing diagnostic due to already reported same sponsorable/product diagnostic");
            ClearExisting(info.ProjectPath);
            return;
        }

        diagnostic = OnDiagnostic(info.ProjectPath, kind);
        if (diagnostic != null)
        {
            Trace($"Save new check: {info.ProjectPath}={kind}");
            SessionManager.Set(info.ProjectPath, kind);

            // Pause if configured so. Note we won't pause if the project is up to date.
            if (diagnostic.Properties.TryGetValue("Pause", out var value) &&
                int.TryParse(value, out var pause) &&
                pause > 0)
            {
                Trace($"Pausing new check for {pause}ms");

#if !CI
                Console.Beep(500, 500);
#endif
                Thread.Sleep(pause);
            }

            // Note that we don't push even Thanks if they were already reported and the 
            // project is up to date. This means the Thanks won't become annoying either.
            Diagnostics.ReportDiagnosticOnce(context,
                Diagnostics.Push(sponsorable, product, info.ProjectPath, diagnostic),
                sponsorable, product);
        }
    }

    void ClearExisting(string projectFile)
    {
        // Clear a previous report in this case, to avoid giving the impression that we
        // paused again when we haven't.
        var cleared = false;
        var objDir = Path.Combine(Path.GetDirectoryName(projectFile), "obj", "SponsorLink", sponsorable, product);
        if (Directory.Exists(objDir))
        {
            foreach (var file in Directory.EnumerateFiles(objDir))
            {
                File.Delete(file);
                cleared = true;
            }
        }

        if (cleared)
        {
            Trace($"Cleared existing diagnostic files");
#if !CI
            Console.Beep(800, 500);
#endif
        }
    }

    /// <summary>
    /// In DTB, we merely re-surface diagnostics that were previously generated in a 
    /// full build. This keeps the checks minimally impactful while still being visible.
    /// </summary>
    void ReportExisting(CompilationAnalysisContext context, string? projectFile)
    {
        var productDir = Path.Combine(Path.GetDirectoryName(projectFile), "obj", "SponsorLink", sponsorable, product);
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
            var diagnostic = Diagnostic.Create(new DiagnosticDescriptor(
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

    (bool warn, int pause, string suffix) GetPause()
    {
        if (settings.InstallTime == null)
            return GetPaused(rnd.Next(settings.PauseMin, settings.PauseMax));

        var daysOld = (int)DateTime.Now.Subtract(settings.InstallTime.Value).TotalDays;

        // Never warn during the quiet days.
        if (daysOld < (settings.QuietDays ?? quietDays))
            return (false, 0, "");

        // At this point, daysOld is greater than quietDays and greater than 1.
        var nonQuietDays = daysOld - (settings.QuietDays ?? quietDays);
        // Turn days pause (starting at 1sec max pause into milliseconds, used for the pause.
        var daysMaxPause = nonQuietDays * 1000;

        // From second day, the max pause will increase from days old until the max pause.
        return GetPaused(rnd.Next(settings.PauseMin, Math.Min(daysMaxPause, settings.PauseMax)));
    }

    static (bool warn, int pause, string suffix) GetPaused(int pause)
        => (true, pause, pause > 0 ? ThisAssembly.Strings.BuildPaused(pause) : "");

    static Diagnostic WriteMessage(string sponsorable, string product, string projectDir, Diagnostic diag)
    {
        try
        {
            var objDir = Path.Combine(projectDir, "obj", "SponsorLink", sponsorable, product);
            if (Directory.Exists(objDir))
            {
                foreach (var file in Directory.EnumerateFiles(objDir))
                    File.Delete(file);
            }

            Directory.CreateDirectory(objDir);
            File.WriteAllText(Path.Combine(objDir, $"{diag.Id}.{diag.Severity}.txt"), diag.GetMessage());
        }
        // This is best-effort only. Cases where it can fail, is when bring run from 
        // the Roslyn analyzer host, which is in program files, and if the project path 
        // is empty, or in a folder that requires admin rights.
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return diag;
    }

    /// <summary>
    /// Provides information about the build that was checked for sponsor linking.
    /// Used internally only for now.
    /// </summary>
    class BuildInfo
    {
        internal BuildInfo(string projectPath, bool? designTimeBuild)
        {
            ProjectPath = projectPath;
            DesignTimeBuild = designTimeBuild;
        }

        /// <summary>
        /// The full path of the project being built.
        /// </summary>
        public string ProjectPath { get; }
        /// <summary>
        /// Whether the build is a design-time build.
        /// </summary>
        public bool? DesignTimeBuild { get; }

        /// <summary>
        /// The installation/restore time of SponsorLink.
        /// </summary>
        public DateTime? InstallTime { get; private set; }

        internal BuildInfo WithInstallTime(DateTime? installTime)
        {
            InstallTime = installTime;
            return this;
        }
    }
}
