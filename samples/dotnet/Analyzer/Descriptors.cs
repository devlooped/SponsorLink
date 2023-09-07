using Microsoft.CodeAnalysis;

namespace SponsorableLib;

/// <summary>
/// Default <see cref="DiagnosticDescriptor"/>s for this sample.
/// </summary>
public static class Descriptors
{
    public static DiagnosticDescriptor MissingProperties { get; } = new("LIB001",
        "Missing configuration",
        "⚠️ Cannot determine sponsorships due to missing MSBuild properties.",
        "Build", DiagnosticSeverity.Warning, true, 
        "This is most likely an actual bug in the package authoring. Please report the issue.",
        helpLinkUri: "https://github.com/devlooped/SponsorLink/issues");

    public static DiagnosticDescriptor ExpiredManifest { get; } = new("LIB002",
        "Expired manifest",
        "⚠️ Sponsorship manifest has expired. Please run 'gh sponsors' again to update.",
        "Build", DiagnosticSeverity.Warning, true, 
        "GitHub sponsorships expire at the end of each month. Run 'gh sponsors' to sync your manifest.",
        helpLinkUri: "https://github.com/devlooped/gh-sponsors");

    public static DiagnosticDescriptor InvalidManifest { get; } = new("LIB003",
        "Invalid manifest",
        "⚠️ Sponsorship manifest is invalid. Please run 'gh sponsors' again to update.",
        "Build", DiagnosticSeverity.Warning, true,
        "The sponsorships manifest seems to be invalid for some reason. Please run 'gh sponsors' to update it.",
        helpLinkUri: "https://github.com/devlooped/gh-sponsors");

    public static DiagnosticDescriptor ManifestNotFound { get; } = new("LIB004",
        "Unknown sponsoring",
        "🙏 This project is looking for sponsorships! Please consider sponsoring and run 'gh sponsors' to sync your sponsorships.",
        "Build", DiagnosticSeverity.Warning, true,
        "GitHub sponsors is a great way to keep the projects you enjoy healthy and sustainable. Learn more by clicking the help link.",
        helpLinkUri: "https://github.com/devlooped/SponsorLink");

    public static DiagnosticDescriptor NotSponsoring { get; } = new("LIB005",
        "Not sponsoring",
        "🙏 This project is looking for sponsorships! Please consider sponsoring and run 'gh sponsors' to update your sponsorships.",
        "Build", DiagnosticSeverity.Warning, true,
        "Thanks for being a SponsorLink user! This project is also seeking funding, learn more at the project repository.",
        helpLinkUri: ThisAssembly.Git.Url);

    /// <summary>
    /// Creates a diagnostic from the given descriptor, replacing the diagnostic ID and help link.
    /// </summary>
    public static Diagnostic Create(this DiagnosticDescriptor descriptor, string? link = null) 
        =>  link == null 
        ? Diagnostic.Create(descriptor, null) 
        : Diagnostic.Create(descriptor.Id, descriptor.Category, descriptor.MessageFormat,
            descriptor.DefaultSeverity, descriptor.DefaultSeverity, true, 4, descriptor.Title,
            descriptor.Description, 
            helpLink: link);
}
