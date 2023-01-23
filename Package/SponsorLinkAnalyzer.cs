using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Devlooped;

/// <summary>
/// Exposes the diagnostics we report so they show up in the IDE with 
/// their full information and links.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic, LanguageNames.FSharp)]
public class SponsorLinkAnalyzer : DiagnosticAnalyzer
{
    internal static DiagnosticDescriptor Broken { get; } = CreateBroken();

    internal static DiagnosticDescriptor AppNotInstalled { get; } = CreateAppNotInstalled();

    internal static DiagnosticDescriptor UserNotSponsoring { get; } = CreateUserNotSponsoring();

    internal static DiagnosticDescriptor Thanks { get; } = CreateThanks();
    
    /// <summary>
    /// Exposes the supported diagnostics.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        // NOTE: for Thanks, since we re-surface it via the analyzer, we can use the proper ID formerly used by the generator.
        CreateBroken("SLI01"), CreateAppNotInstalled("SLI02"), CreateUserNotSponsoring("SLI03"), CreateThanks("SLI04"));

    static DiagnosticDescriptor CreateThanks(string id = "SL04") => new(
        id,
        "You are a sponsor of the project, you rock 💟!",
        "Thank you for supporting {0} with your sponsorship of {1} 💟!",
        "SponsorLink",
        DiagnosticSeverity.Info,
        true,
        "You are a true hero. Your sponsorship helps keep the project alive and thriving.",
        "https://github.com/sponsors",
        "DoesNotSupportF1Help");

    static DiagnosticDescriptor CreateUserNotSponsoring(string id = "SL03") => new(
        id,
        "Please consider supporting the ongoing development of the project 🙏",
        "Please consider supporting {0} ongoing development by sponsoring at https://github.com/sponsors/{1}. Build paused for {2}ms.",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Sponsoring projects you depend on ensures they remain active, and that you get the support you need.",
        "https://github.com/sponsors",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    static DiagnosticDescriptor CreateAppNotInstalled(string id = "SL02") => new(
        id,
        "Please install the SponsorLink GitHub app 🙏",
        "{0} uses SponsorLink to properly attribute your sponsorship with {1}. Please install the GitHub app at https://github.com/apps/sponsorlink. Build paused for {2}ms.",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Installing the SponsorLink GitHub app ensures that your sponsorship is properly attributed to you.",
        "https://github.com/apps/sponsorlink",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    static DiagnosticDescriptor CreateBroken(string id = "SL01") => new(
        id,
        "Invalid SponsorLink configuration 🤔",
        "SponsorLink has been incorrectly configured. Please check the documentation for more information.",
        "SponsorLink",
        DiagnosticSeverity.Error,
        true,
        "A library author or custom tweaks to your MSBuild projects and targets seems to have broken SponsorLink.",
        "https://github.com/devlooped/SponsorLink/discussions",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable("DEBUG_SPONSORLINK"), out var debug) && debug)
            if (Debugger.IsAttached)
                Debugger.Break();
            else
                Debugger.Launch();

        var opt = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (!opt.TryGetValue("build_property.MSBuildProjectFullPath", out var projectPath))
            return;

        // Locate all info and report them again?
        var objDir = Path.Combine(Path.GetDirectoryName(projectPath), "obj", "SponsorLink");
        if (!Directory.Exists(objDir))
            return;

        foreach (var sponsorableDir in Directory.EnumerateDirectories(objDir))
        {
            var sponsorable = new DirectoryInfo(sponsorableDir).Name;
            foreach (var projectDir in Directory.EnumerateDirectories(sponsorableDir))
            {
                var product = new DirectoryInfo(projectDir).Name;
                if (File.Exists(Path.Combine(projectDir, $"{Thanks.Id}.{Thanks.DefaultSeverity}.txt")))
                    context.ReportDiagnostic(Diagnostic.Create(CreateThanks("SLI04"),
                        // If we provide a non-null location, the message for some reason is no longer shown in VS :/
                        null,
                        product, sponsorable));
            }
        }
    }
}
