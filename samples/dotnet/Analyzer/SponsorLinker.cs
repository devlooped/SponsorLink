using System;
using System.IO;
using Devlooped;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static Devlooped.SponsorLink;

// Showcases manual annotation at the assembly level.
[assembly: Funding("devlooped")]
[assembly: Funding("kzu")]

namespace SponsorableLib;

/// <summary>
/// Incremental source generators are guaranteed to run, unlike analyzers.
/// </summary>
[Generator(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class SponsorLinker : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => context.RegisterSourceOutput(context.AnalyzerConfigOptionsProvider,
            (c, t) => CheckAndReport(t.GlobalOptions, c.ReportDiagnostic));

    bool CheckAndReport(AnalyzerConfigOptions options, Action<Diagnostic> report)
    {
        // This can be helpful to step through what is actually happening, from another IDE instance, 
        // i.e. in Rider. But for VS, just F5 on this solution Just Works :)
        if (Environment.GetEnvironmentVariable("DEBUG_SPONSORABLELIB", EnvironmentVariableTarget.User) == "1")
            System.Diagnostics.Debugger.Launch();

        // Do nothing if we're not running within the editor
        if (!IsEditor)
            return false;

        if (!options.TryGetValue("build_property.SponsorableLibSponsorable", out var path) ||
            // ProjectDir is brought by Microsoft.NET.Sdk.Analyzers.targets automatically
            !options.TryGetValue("build_property.ProjectDir", out var dir) ||
            !File.Exists(path))
        {
            // We can't detect anything without access to those variables. 
            // TODO: should we let the user know? An Info diagnostics saying Sponsorable features may be disabled?
            report(Diagnostic.Create(Descriptors.MissingProperties, null));
            return true;
        }

        // NOTE: pre-17.8, there's a Roslyn bug that causes the warning to show twice
        // in VS: https://github.com/dotnet/roslyn/issues/69826

        // NOTE: you can either hardcode a specific sponsorable account or rely on
        // assembly-level attributes (FundingAttribute) which by default are added
        // automatically by reading the .github/FUNDING.yml file, if any.
        // Getting the creation time of a file in the package allows checking for 
        // the age of the installed package, to potentially turn on/off features.
        // That's optional, of course.
        SponsorLink.Initialize(dir, FundingAccounts, File.GetCreationTime(path));

        // Change to a URI for use in help links
        var link = "file://" + path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // NOTE: consumers have full control over what they do once the sponsoring 
        // status has been determined. SL package itself does nothing but provide some helpers.
        var diagnostic = Status switch
        {
            ManifestStatus.Expired => Descriptors.ExpiredManifest.Create(),
            ManifestStatus.Invalid => Descriptors.InvalidManifest.Create(),
            // This showcases how to use a package-provided file as a link.                
            ManifestStatus.NotFound => Descriptors.ManifestNotFound.Create(link),
            ManifestStatus.Verified when IsSponsor == false => Descriptors.NotSponsoring.Create(),
            _ => null,
        };

        if (diagnostic != null)
        {
            report(diagnostic);
            return true;
        }

        return false;
    }
}