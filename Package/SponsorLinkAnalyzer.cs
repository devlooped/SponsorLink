using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped;

/// <summary>
/// Exposes the diagnostics we report so they show up in the IDE with 
/// their full information and links.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic, LanguageNames.FSharp)]
public class SponsorLinkAnalyzer : DiagnosticAnalyzer
{
    internal static DiagnosticDescriptor Broken { get; } = new DiagnosticDescriptor(
        "SL01",
        "Invalid SponsorLink configuration",
        "SponsorLink has been incorrectly configured. Please check the documentation for more information.",
        "SponsorLink",
        DiagnosticSeverity.Error,
        true,
        "A library author or custom tweaks to your MSBuild projects and targets seems to have broken SponsorLink.",
        "https://github.com/devlooped/SponsorLink/discussions",
        "DoesNotSupportF1Help");

    internal static DiagnosticDescriptor AppNotInstalled { get; } = new DiagnosticDescriptor(
        "SL02",
        "SponsorLink GitHub app should be installed",
        "{0} uses SponsorLink to properly attribute your sponsorship with {1}. Please install the GitHub app at https://github.com/apps/sponsorlink.",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Installing the SponsorLink GitHub app ensures that your sponsorship is properly attributed to you.",
        "https://github.com/apps/sponsorlink",
        "DoesNotSupportF1Help");

    internal static DiagnosticDescriptor UserNotSponsoring { get; } = new DiagnosticDescriptor(
        "SL03",
        "You are not sponsoring the ongoing development of the project",
        "Please consider supporting {0} development by sponsoring it at https://github.com/sponsors/{1}.",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Sponsoring projects you depend on ensures they remain active, and that you get the support you need.",
        "https://github.com/sponsors",
        "DoesNotSupportF1Help");

    internal static DiagnosticDescriptor Thanks { get; } = new DiagnosticDescriptor(
        "SL04",
        "You are a sponsor of the project, you rock 💟!",
        "Thank you for supporting {0} with your sponsorship of {1} 💟!",
        "SponsorLink",
        DiagnosticSeverity.Info,
        true,
        "You are a true hero. Your sponsorship helps keep the project alive and thriving.",
        "https://github.com/sponsors",
        "DoesNotSupportF1Help");

    /// <summary>
    /// Exposes the supported diagnostics.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Broken, AppNotInstalled, UserNotSponsoring, Thanks);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    }
}
