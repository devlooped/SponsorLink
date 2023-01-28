extern alias Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

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
    public void CreateSponsorLink()
    {
        IIncrementalGenerator link = new TestSponsorLink();
        // Due to improper init on the context, this should fail with an NRE
        Assert.Throws<NullReferenceException>(() => link.Initialize(new IncrementalGeneratorInitializationContext()));
    }
}
