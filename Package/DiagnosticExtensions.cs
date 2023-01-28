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
    /// Checks whether the given <paramref name="descriptor"/> will be used for the given 
    /// <paramref name="kind"/> diagnostic kind when the default <see cref="SponsorLink.OnDiagnostic(string, DiagnosticKind)"/>
    /// executes.
    /// </summary>
    public static bool IsKind(this DiagnosticDescriptor descriptor, DiagnosticKind kind) =>
        descriptor.CustomTags.Contains(kind.ToString());

    /// <summary>
    /// Creates a copy of the given <paramref name="descriptor"/> with selected values replaced.
    /// </summary>
    /// <param name="descriptor">The original descriptor.</param>
    /// <param name="id">A unique identifier for the diagnostic. For example, code analysis diagnostic ID "CA1001".</param>
    /// <param name="title">A short localizable title describing the diagnostic. For example, for CA1001: "Types that own disposable fields should be disposable".</param>
    /// <param name="messageFormat">A localizable format message string, which can be passed as the first argument to <see cref="string.Format(string, object[])"/> when creating the diagnostic message with this descriptor.
    /// For example, for CA1001: "Implement IDisposable on '{0}' because it creates members of the following IDisposable types: '{1}'."</param>
    /// <param name="description">An optional longer localizable description of the diagnostic.</param>
    /// <param name="helpLinkUri">An optional hyperlink that provides a more detailed description regarding the diagnostic.</param>
    public static DiagnosticDescriptor With(
        this DiagnosticDescriptor descriptor,
        string? id = default,
        LocalizableString? title = default,
        LocalizableString? messageFormat = default,
        LocalizableString? description = default,
        string? helpLinkUri = default)
        => new DiagnosticDescriptor(
            id: id ?? descriptor.Id,
            title: title ?? descriptor.Title,
            messageFormat: messageFormat ?? descriptor.MessageFormat,
            description: description ?? descriptor.Description,
            helpLinkUri: helpLinkUri ?? descriptor.HelpLinkUri,
            // immutable values
            category: descriptor.Category,
            defaultSeverity: descriptor.DefaultSeverity,
            isEnabledByDefault: descriptor.IsEnabledByDefault,
            customTags: descriptor.CustomTags.ToArray());
}
