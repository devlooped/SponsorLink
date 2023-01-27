using Devlooped;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SponsorableLib;

[Generator]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
class SponsorLinker : SponsorLink
{
    public SponsorLinker() : base("kzu", "SponsorableLib") 
        // NOTE: diagnostics prefix will default to K(zu)S(Sponsorable)L(Lib) > DSLxx
    { }
}