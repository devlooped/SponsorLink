extern alias Analyzer;

using Microsoft.CodeAnalysis;

namespace Devlooped;

public class AnalyzerTests
{
    [Trait("SponsorLink", "true")]
    [Fact]
    public void CreateSponsorLink()
    {
        var link = new Analyzer::Devlooped.SponsorLink("foo", "bar");

        // Due to improper init on the context, this should fail with an NRE
        Assert.Throws<NullReferenceException>(() => link.Initialize(new IncrementalGeneratorInitializationContext()));
    }
}
