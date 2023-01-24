using Devlooped;
using Microsoft.CodeAnalysis;

namespace SponsorableLib;

[Generator]
class Generator : IIncrementalGenerator
{
    readonly SponsorLink link;

    // TODO: replace with your sponsorable account and library name.
    // NOTE: we can also configure the min/max milliseconds for the random build pause, 
    // or go full customized diagnostics by passing in delegates for the various events.
    public Generator() => link = new("devlooped", "SponsorableLib");

    public void Initialize(IncrementalGeneratorInitializationContext context) => link.Initialize(context);
}
