using Devlooped;
using Microsoft.CodeAnalysis;

namespace SponsorableLib;

[Generator]
public class Generator : IIncrementalGenerator
{
    readonly SponsorLink link;

    public Generator() => link = new("[SPONSORABLE]", "MyLib");

    public void Initialize(IncrementalGeneratorInitializationContext context) => link.Initialize(context);
}
