using System.Collections.Immutable;
using System.Linq;
using Devlooped;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SponsorableLib;

[Generator]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
class SimpleSponsorLinker : SponsorLink
{
    public SimpleSponsorLinker() : base("test", "SponsorableLib")
    // NOTE: diagnostics prefix will default to T(est)S(Sponsorable)L(Lib) > TSLxx
    // NOTE: since we don't specify any Quiet Days via settings, we will get the 
    // default behavior (15 days), meaning no warnings will be reported for this 
    // analyzer when built locally. 
    { }
}


[Generator]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
class AdvancedSponsorLinker : SponsorLink
{
    static readonly SponsorLinkSettings settings;
    
    static AdvancedSponsorLinker()
    {
        // NOTE: diagnostics prefix will default to T(test)S(Sponsorable)L(Lib) > TSLxx
        settings = SponsorLinkSettings.Create("test", "AdvancedSponsorableLib",
            packageId: "SponsorableLib",
            // This introduces warnings right from the start. The 
            // default pauses always start from the second non-quiet day, 
            // and increase from zero (max pause) until configured 
            // max pause, increasing by 1sec (max pause) per additional 
            // day of usage. So, on the 2nd day, the pause will be random
            // between 0ms and 1000ms (1 day after quiet days). The following 
            // day it will be up to 2000ms pause, and so on until the max 
            // configured pause, which defaults to 4000ms.
            quietDays: 0);
        // Here we showcase how to modify the built-in diagnostics to add a custom description.
        settings.SupportedDiagnostics = settings.SupportedDiagnostics
            .Select(x => x.IsKind(DiagnosticKind.UserNotSponsoring) ?
                x.With(description: "Your sponsorship is used to further develop SponsorLink and make it great for the entire oss community!") :
                x)
            .ToImmutableArray();
    }

    public AdvancedSponsorLinker() : base(settings) { } 

    // Do something different on diagnostic instead of reporting? Can be anything.
    protected override Diagnostic? OnDiagnostic(string projectPath, DiagnosticKind kind) 
        => base.OnDiagnostic(projectPath, kind);
}