using System.ComponentModel;
using Microsoft.CodeAnalysis;

namespace Devlooped;

/// <summary>
/// Adds diagnostic mutating extensions.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DiagnosticExtensions
{
    /// <summary>
    /// Returns a clone of <paramref name="diagnostic"/> with given <paramref name="description"/>.
    /// </summary>
    public static Diagnostic WithDescription(this Diagnostic diagnostic, LocalizableString description)
        => Diagnostic.Create(new DiagnosticDescriptor(
                        id: diagnostic.Id,
                        title: diagnostic.Descriptor.Title,
                        messageFormat: diagnostic.GetMessage(),
                        category: diagnostic.Descriptor.Category,
                        defaultSeverity: diagnostic.Descriptor.DefaultSeverity,
                        isEnabledByDefault: diagnostic.Descriptor.IsEnabledByDefault,
                        description: description,
                        helpLinkUri: diagnostic.Descriptor.HelpLinkUri,
                        customTags: diagnostic.Descriptor.CustomTags.ToArray()),
                        diagnostic.Location, 
                        diagnostic.AdditionalLocations,
                        properties: diagnostic.Properties);

    /// <summary>
    /// Returns a clone of <paramref name="diagnostic"/> with given <paramref name="description"/>.
    /// </summary>
    public static Diagnostic WithDescription(this Diagnostic diagnostic, string description)
        => Diagnostic.Create(new DiagnosticDescriptor(
                        id: diagnostic.Id,
                        title: diagnostic.Descriptor.Title,
                        messageFormat: diagnostic.GetMessage(),
                        category: diagnostic.Descriptor.Category,
                        defaultSeverity: diagnostic.Descriptor.DefaultSeverity,
                        isEnabledByDefault: diagnostic.Descriptor.IsEnabledByDefault,
                        description: description,
                        helpLinkUri: diagnostic.Descriptor.HelpLinkUri,
                        customTags: diagnostic.Descriptor.CustomTags.ToArray()),
                        diagnostic.Location,
                        diagnostic.AdditionalLocations,
                        properties: diagnostic.Properties);
}
