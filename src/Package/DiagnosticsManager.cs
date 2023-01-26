using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped;

enum DiagnosticKind { AppNotInstalled, UserNotSponsoring, Thanks }

class DiagnosticsManager
{
    ConcurrentDictionary<string, string> SponsorableIdPrefixes
    {
        get => AppDomainDictionary.Get<ConcurrentDictionary<string, string>>(nameof(SponsorableIdPrefixes));
    }

    ConcurrentDictionary<(string Sponsorable, string IdPrefix, DiagnosticKind Kind), DiagnosticDescriptor> Descriptors
    {
        get => AppDomainDictionary.Get<ConcurrentDictionary<(string Sponsorable, string IdPrefix, DiagnosticKind Kind), DiagnosticDescriptor>>(nameof(Descriptors));
    }

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

    public DiagnosticDescriptor GetDescriptor(string sponsorable, string idPrefix, DiagnosticKind kind)
    {
        // NOTE: ID prefixes are per sponsorable, not per product, since the URLs 
        // are unique per sponsorable, not product.
        SponsorableIdPrefixes.TryAdd(sponsorable, idPrefix);
        return Descriptors.GetOrAdd((sponsorable, idPrefix, kind),
            key => key.Kind switch
            {
                DiagnosticKind.AppNotInstalled => CreateAppNotInstalled(key.IdPrefix),
                DiagnosticKind.UserNotSponsoring => CreateUserNotSponsoring(key.Sponsorable, key.IdPrefix),
                DiagnosticKind.Thanks => CreateThanks(key.Sponsorable, key.IdPrefix),
                _ => throw new NotImplementedException(),
            });
    }

    public Diagnostic Push(string sponsorable, string product, string project, DiagnosticKind kind, params object?[]? args)
    {
        var descriptor = GetDescriptor(sponsorable,
            // Backwards-compatible gets potentially same descriptor IDs for distinct sponsorables.
            // The actual behavior will be somewhat inconsistent in that the links might end up 
            // pointing to the general /sponsors page rather than a specific sponsorable account one, 
            // but otherwise, it's harmless
            SponsorableIdPrefixes.TryGetValue(sponsorable, out var prefix) ? prefix : "",
            kind);

        // If we provide a non-null location, the entry for some reason is no longer shown in VS :/
        var diagnostic = Diagnostic.Create(descriptor,
            //Location.Create(project, new(), new()),
            null,
            args);
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

    static DiagnosticDescriptor CreateThanks(string sponsorable, string prefix) => new(
        $"{prefix}SL04",
        "You are a sponsor of the project, you rock 💟!",
        "Thank you for supporting {0} with your sponsorship of {1} 💟!",
        "SponsorLink",
        DiagnosticSeverity.Info,
        true,
        "You are a true hero. Your sponsorship helps keep the project alive and thriving.",
        "https://github.com/sponsors/" + sponsorable,
        "DoesNotSupportF1Help");

    static DiagnosticDescriptor CreateUserNotSponsoring(string sponsorable, string prefix) => new(
        $"{prefix}SL03",
        "Please consider supporting the ongoing development of the project 🙏",
        "Please consider supporting {0} ongoing development by sponsoring at https://github.com/sponsors/{1}. {2}",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Sponsoring projects you depend on ensures they remain active, and that you get the support you need. It's also super affordable and available worldwide!",
        "https://github.com/sponsors/" + sponsorable,
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    static DiagnosticDescriptor CreateAppNotInstalled(string prefix) => new(
        $"{prefix}SL02",
        "Please install the SponsorLink GitHub app 🙏",
        "{0} uses SponsorLink to properly attribute your sponsorship with {1}. Please install the GitHub app at https://github.com/apps/sponsorlink. {2}",
        "SponsorLink",
        DiagnosticSeverity.Warning,
        true,
        "Installing the SponsorLink GitHub app ensures that your sponsorship is properly attributed to you.",
        "https://github.com/apps/sponsorlink",
        "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable);

    static DiagnosticDescriptor CreateBroken() => new(
        "SL01",
        "Invalid SponsorLink configuration 🤔",
        "SponsorLink has been incorrectly configured. Please check the documentation for more information.",
        "SponsorLink",
        DiagnosticSeverity.Error,
        true,
        "A library author or custom tweaks to your MSBuild projects and targets seems to have broken SponsorLink.",
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
