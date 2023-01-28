﻿using System.Collections.Immutable;
using System.Linq;
using Devlooped;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SponsorableLib;

[Generator]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
class SimpleSponsorLinker : SponsorLink
{
    public SimpleSponsorLinker() : base("kzu", "SponsorableLib")
    // NOTE: diagnostics prefix will default to K(zu)S(Sponsorable)L(Lib) > DSLxx
    { }
}


[Generator]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
class AdvancedSponsorLinker : SponsorLink
{
    static readonly SponsorLinkSettings settings;
    
    static AdvancedSponsorLinker()
    {
        // NOTE: diagnostics prefix will default to K(zu)S(Sponsorable)L(Lib) > DSLxx
        settings = SponsorLinkSettings.Create("kzu", "AdvancedSponsorableLib");
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