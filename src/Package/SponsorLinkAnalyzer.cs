using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped;

enum DescriptorKind { Analyzer, Generator }

/// <summary>
/// Exposes the diagnostics we report so they show up in the IDE with 
/// their full information and links.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic, LanguageNames.FSharp)]
public class SponsorLinkAnalyzer : DiagnosticAnalyzer
{
    internal static DiagnosticDescriptor Broken { get; } = CreateBroken(DescriptorKind.Analyzer);
    internal static DiagnosticDescriptor AppNotInstalled { get; } = CreateAppNotInstalled(DescriptorKind.Analyzer);
    internal static DiagnosticDescriptor UserNotSponsoring { get; } = CreateUserNotSponsoring(DescriptorKind.Analyzer);
    internal static DiagnosticDescriptor Thanks { get; } = CreateThanks(DescriptorKind.Analyzer);

    /// <summary>
    /// Exposes the supported diagnostics.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Broken, AppNotInstalled, UserNotSponsoring, Thanks);

    internal static DiagnosticDescriptor CreateThanks(DescriptorKind kind) => new(
        kind == DescriptorKind.Analyzer ? "SL04" : "SLI04",
        "You are a sponsor of the project, you rock 💟!",
        "Thank you for supporting {0} with your sponsorship of {1} 💟!",
        "SponsorLink",
        DiagnosticSeverity.Info,
        true,
        "You are a true hero. Your sponsorship helps keep the project alive and thriving.",
        "https://github.com/sponsors",
        "DoesNotSupportF1Help");

    internal static DiagnosticDescriptor CreateUserNotSponsoring(DescriptorKind kind) => new(
        kind == DescriptorKind.Analyzer ? "SL03" : "SLI03",
        "Please consider supporting the ongoing development of the project 🙏",
        "Please consider supporting {0} ongoing development by sponsoring at https://github.com/sponsors/{1}. Build paused for {2}ms.",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Sponsoring projects you depend on ensures they remain active, and that you get the support you need.",
        "https://github.com/sponsors",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    internal static DiagnosticDescriptor CreateAppNotInstalled(DescriptorKind kind) => new(
        kind == DescriptorKind.Analyzer ? "SL02" : "SL02",
        "Please install the SponsorLink GitHub app 🙏",
        "{0} uses SponsorLink to properly attribute your sponsorship with {1}. Please install the GitHub app at https://github.com/apps/sponsorlink. Build paused for {2}ms.",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Installing the SponsorLink GitHub app ensures that your sponsorship is properly attributed to you.",
        "https://github.com/apps/sponsorlink",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    internal static DiagnosticDescriptor CreateBroken(DescriptorKind kind) => new(
        kind == DescriptorKind.Analyzer ? "SL01" : "SL01",
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

        // Surfaces written diagnostics messages as analyer diagnostics. 
        // Seems to provide working links!
        foreach (var sponsorableDir in Directory.EnumerateDirectories(objDir))
        {
            var sponsorable = new DirectoryInfo(sponsorableDir).Name;
            foreach (var projectDir in Directory.EnumerateDirectories(sponsorableDir))
            {
                var product = new DirectoryInfo(projectDir).Name;
                foreach (var file in Directory.EnumerateFiles(projectDir, "*.txt"))
                {
                    var parts = Path.GetFileName(file).Split('.');
                    if (parts.Length < 2)
                        continue;

                    // We always report here, since this is the easiest to disable by 
                    // users. The generator will check for these being disabled and 
                    // emit its own in turn (since that's harder to disable becuase 
                    // it comes with the same assembly as the consuming project.
                    var id = parts[0];
                    var severity = parts[1];
                    var descriptor = SupportedDiagnostics.FirstOrDefault(x => x.Id == id);
                    if (descriptor == null)
                        continue;

                    // Turn the original format string into a regex to match against the actual 
                    // string, so we can recreate it for a new diagnostic.
                    var regex = new Regex(Regex.Replace(
                        descriptor.MessageFormat.ToString(),
                        @"\{(?<i>\d+)\}",
                        // The group name must start with a letter, but we know it's an index.
                        @"(?<a${i}>.+?)"));

                    var text = File.ReadAllText(file).Trim();
                    var match = regex.Match(text);
                    var args = regex.GetGroupNames()
                        .Where(x => char.IsLetter(x[0]))
                        .Select(x => new { Index = int.Parse(x.Substring(1)), match.Groups[x].Value })
                        .OrderBy(x => x.Index)
                        .Select(x => x.Value)
                        .ToArray();

                    // Match text using a regex to find if it contains a URL
                    var url = Regex.Match(text, @"http.*?\s").Value.Trim().TrimEnd('.');
                    // Create a linkable diagnostics if there is a URL in the message.
                    if (!string.IsNullOrEmpty(url))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                            id: descriptor.Id,
                            title: descriptor.Title, 
                            messageFormat: descriptor.MessageFormat,
                            category: descriptor.Category,
                            defaultSeverity: descriptor.DefaultSeverity, 
                            isEnabledByDefault: descriptor.IsEnabledByDefault,
                            description: descriptor.Description, 
                            helpLinkUri: url,
                            customTags: descriptor.CustomTags.ToArray()),
                            // If we provide a non-null location, the entry for some reason is no longer shown in VS :/
                            null,
                            args));
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            descriptor,
                            // If we provide a non-null location, the entry for some reason is no longer shown in VS :/
                            null,
                            args));
                    }
                }
            }
        }
    }
}
