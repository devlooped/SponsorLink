using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped;


class DiagnosticsManager
{
    ConcurrentDictionary<(string, string, string), Diagnostic> Diagnostics
    {
        get => AppDomainDictionary.Get<ConcurrentDictionary<(string, string, string), Diagnostic>>(nameof(Diagnostics));
    }

    ConcurrentDictionary<(string, string), SyntaxTreeValueProvider<HashSet<string>>> ValueProviders
    {
        get => AppDomainDictionary.Get<ConcurrentDictionary<(string, string), SyntaxTreeValueProvider<HashSet<string>>>>(nameof(ValueProviders));
    }

    public static DiagnosticDescriptor Broken { get; } = CreateBroken();

    public void ReportDiagnosticOnce(CompilationAnalysisContext context, Diagnostic diagnostic, string sponsorable, string product)
    {
        var provider = ValueProviders.GetOrAdd((sponsorable, product),
            _ => new SyntaxTreeValueProvider<HashSet<string>>(_ => new()));

        if (context.TryGetValue(context.Compilation.SyntaxTrees.First(), provider, out var ids) && !ids.Contains(diagnostic.Id))
        {
            lock (ids)
            {
                context.ReportDiagnostic(diagnostic);
                ids.Add(diagnostic.Id);
            }
        }
    }

    public ImmutableArray<DiagnosticDescriptor> GetDescriptors(string sponsorable, string idPrefix) => ImmutableArray.Create(
            GetDescriptor(sponsorable, idPrefix, DiagnosticKind.AppNotInstalled),
            GetDescriptor(sponsorable, idPrefix, DiagnosticKind.UserNotSponsoring),
            GetDescriptor(sponsorable, idPrefix, DiagnosticKind.Thanks));

    public DiagnosticDescriptor GetDescriptor(string sponsorable, string idPrefix, DiagnosticKind kind) => kind switch
    {
        DiagnosticKind.AppNotInstalled => CreateAppNotInstalled(idPrefix),
        DiagnosticKind.UserNotSponsoring => CreateUserNotSponsoring(sponsorable, idPrefix),
        DiagnosticKind.Thanks => CreateThanks(sponsorable, idPrefix),
        _ => throw new NotImplementedException(),
    };

    public Diagnostic Push(string sponsorable, string product, string project, Diagnostic diagnostic)
    {
        // Directly sets, since we only expect to get one warning per sponsorable+product+project 
        // combination.
        Diagnostics[(sponsorable, product, project)] = diagnostic;
        return diagnostic;
    }

    public Diagnostic? Pop(string sponsorable, string product, string project)
    {
        Diagnostics.TryRemove((sponsorable, product, project), out var diagnostic);
        return diagnostic;
    }

    public bool TryPeek(string sponsorable, string product, string project, out Diagnostic? diagnostic)
        => Diagnostics.TryGetValue((sponsorable, product, project), out diagnostic);

    static DiagnosticDescriptor CreateAppNotInstalled(string prefix) => new(
        $"{prefix}02",
        ThisAssembly.Strings.AppNotInstalled.Title,
        ThisAssembly.Strings.AppNotInstalled.MessageFormat,
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        ThisAssembly.Strings.AppNotInstalled.Description,
        "https://github.com/apps/sponsorlink",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable, nameof(DiagnosticKind.AppNotInstalled));

    static DiagnosticDescriptor CreateUserNotSponsoring(string sponsorable, string prefix) => new(
     $"{prefix}03",
     ThisAssembly.Strings.UserNotSponsoring.Title,
     ThisAssembly.Strings.UserNotSponsoring.MessageFormat,
     "SponsorLink",
     DiagnosticSeverity.Warning,
     true,
     ThisAssembly.Strings.UserNotSponsoring.Description,
     "https://github.com/sponsors/" + sponsorable,
     "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable, nameof(DiagnosticKind.UserNotSponsoring));

    static DiagnosticDescriptor CreateThanks(string sponsorable, string prefix) => new(
        $"{prefix}04",
        ThisAssembly.Strings.Thanks.Title,
        ThisAssembly.Strings.Thanks.MessageFormat,
        "SponsorLink",
        DiagnosticSeverity.Info,
        true,
        ThisAssembly.Strings.Thanks.Description,
        "https://github.com/sponsors/" + sponsorable,
        "DoesNotSupportF1Help", nameof(DiagnosticKind.Thanks));

    static DiagnosticDescriptor CreateBroken() => new(
        "SL01",
        ThisAssembly.Strings.Broken.Title,
        ThisAssembly.Strings.Broken.Message,
        "SponsorLink",
        DiagnosticSeverity.Error,
        true,
        ThisAssembly.Strings.Broken.Description,
        "https://github.com/devlooped/SponsorLink/discussions",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    class AlwaysEqual : IEqualityComparer<SyntaxTree>
    {
        public static IEqualityComparer<SyntaxTree> Default { get; } = new AlwaysEqual();
        public AlwaysEqual() { }
        public bool Equals(SyntaxTree x, SyntaxTree y) => true;
        public int GetHashCode(SyntaxTree obj) => 0;
    }
}
