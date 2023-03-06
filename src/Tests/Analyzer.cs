extern alias Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Devlooped;

[Generator]
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestSponsorLink : Analyzer::Devlooped.SponsorLink
{
    public TestSponsorLink() : base(Analyzer::Devlooped.SponsorLinkSettings.Create("foo", "bar"))
    {
    }
}

public class AnalyzerTests
{
    [Trait("SponsorLink", "true")]
    [Fact]
    public async Task CreateSponsorLinkAsync()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<TestSponsorLink, XUnitVerifier>();

        test.TestCode = "// ";
        test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerError("SL01"));

        await test.RunAsync();
    }
}
