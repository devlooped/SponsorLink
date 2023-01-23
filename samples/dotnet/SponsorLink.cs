using Devlooped;
using Microsoft.CodeAnalysis;

namespace SponsorableLib;

[Generator]
public class Generator : IIncrementalGenerator
{
    readonly SponsorLink link;

    // TODO: replace with your sponsorable account and library name.
    public Generator() => link = new("devlooped", "SponsorableLib");

    public void Initialize(IncrementalGeneratorInitializationContext context) => link.Initialize(context);
}
