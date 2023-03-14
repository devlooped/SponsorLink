using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Devlooped;

/// <summary>
/// Configures the default behavior of the <see cref="SponsorLink"/> analyzer/generator.
/// </summary>
/// <remarks>
/// The default behavior configured by this class is applied in the 
/// <see cref="SponsorLink.OnDiagnostic(string, DiagnosticKind)"/> virtual method. 
/// If overriden, none of these settings are actually used.
/// </remarks>
public class SponsorLinkSettings
{
    const int DefaultMaxPause = 4000;

    // Make this private for now. We may expose derived types if needed at some point?
    SponsorLinkSettings(string sponsorable, string product)
    {
        Sponsorable = sponsorable;
        Product = product;
    }


    /// <summary>
    /// Creates the settings for <see cref="SponsorLink"/> with the given values.
    /// </summary>
    /// <param name="sponsorable">The sponsor account to check for sponsorships.</param>
    /// <param name="product">The product that uses SponsorLink. Used in diagnostics to clarify the product requesting the sponsor link check.</param>
    public static SponsorLinkSettings Create(string sponsorable, string product)
        => Create(sponsorable, product,
            packageId: default,
            version: default,
            diagnosticsIdPrefix: default,
            pauseMin: default,
            pauseMax: DefaultMaxPause,
            quietDays: default);

    /// <summary>
    /// Creates the settings for <see cref="SponsorLink"/> with the given values.
    /// </summary>
    /// <param name="sponsorable">The sponsor account to check for sponsorships.</param>
    /// <param name="product">The product that uses SponsorLink. Used in diagnostics to clarify the product requesting the sponsor link check.</param>
    /// <param name="packageId">Optional NuGet package identifier of the product performing the check. Defaults to <paramref name="product"/>. 
    /// Used to determine installation time of the product and avoid pausing builds or emitting warnings during the 
    /// quiet days after install (configurable using another overload).</param>
    /// <param name="diagnosticsIdPrefix">Prefix to use for diagnostics with numbers <c>02,03,04</c> reported by default. If not provided, 
    /// a default one is determined from the <paramref name="sponsorable"/> and <paramref name="product"/> values.</param>
    /// <param name="pauseMin">Min random milliseconds to apply during build for non-sponsoring users. Use 0 for no pause.</param>
    /// <param name="pauseMax">Max random milliseconds to apply during build for non-sponsoring users. Use 0 for no pause.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static SponsorLinkSettings Create(string sponsorable, string product,
        string? packageId = default,
        string? diagnosticsIdPrefix = default,
        int pauseMin = 0, 
        int pauseMax = DefaultMaxPause) => Create(sponsorable, product,
            packageId: packageId,
            diagnosticsIdPrefix: diagnosticsIdPrefix,
            version: default,
            pauseMin: pauseMin,
            pauseMax: pauseMax,
            quietDays: default);

    /// <summary>
    /// Creates the settings for <see cref="SponsorLink"/> with the given values.
    /// </summary>
    /// <param name="sponsorable">The sponsor account to check for sponsorships.</param>
    /// <param name="product">The product that uses SponsorLink. Used in diagnostics to clarify the product requesting the sponsor link check.</param>
    /// <param name="packageId">Optional NuGet package identifier of the product performing the check. Defaults to <paramref name="product"/>. 
    /// Used to determine installation time of the product and avoid pausing builds or emitting warnings during the 
    /// <paramref name="quietDays"/> after install.</param>
    /// <param name="version">Optional product or package version.</param>
    /// <param name="diagnosticsIdPrefix">Prefix to use for diagnostics with numbers <c>02,03,04</c> reported by default. If not provided, 
    /// a default one is determined from the <paramref name="sponsorable"/> and <paramref name="product"/> values.</param>
    /// <param name="pauseMin">Min random milliseconds to apply during build for non-sponsoring users. Use 0 for no pause.</param>
    /// <param name="pauseMax">Max random milliseconds to apply during build for non-sponsoring users. Use 0 for no pause.</param>
    /// <param name="quietDays">Optional days to keep warnings quiet so the user has a chance to test the product undisturbed.</param>
    public static SponsorLinkSettings Create(string sponsorable, string product,
        string? packageId = default,
        string? version = default,
        string? diagnosticsIdPrefix = default,
        int pauseMin = 0,
        int pauseMax = DefaultMaxPause,
        int? quietDays = default)
    {
        if (quietDays < 0)
            // Throwing would be a backwards incompatible change.
            quietDays = 0;

        if (diagnosticsIdPrefix == null)
        {
            var sb = new StringBuilder();
            var chars = sponsorable.Where(char.IsUpper).ToArray();
            if (chars.Length == 0)
            {
                sb.Append(char.ToUpper(sponsorable[0]));
            }
            else if (chars.Length == 1)
            {
                sb.Append(chars[0]);
            }
            else if (chars.Length >= 2)
            {
                sb.Append(chars[1]);
            }

            chars = product.Where(char.IsUpper).ToArray();
            if (chars.Length == 0)
            {
                sb.Append(char.ToUpper(product[0]));
            }
            else
            {
                // Append chars as uppercase until sb is at most 4 chars long
                for (var i = 0; i < chars.Length && sb.Length < 4; i++)
                    sb.Append(chars[i]);
            }

            diagnosticsIdPrefix = sb.ToString();
        }

        return new SponsorLinkSettings(sponsorable, product)
        {
            PackageId = packageId ?? product,
            Version = version,
            PauseMin = pauseMin,
            PauseMax = pauseMax,
            QuietDays = quietDays,
            SupportedDiagnostics = SponsorLink.Diagnostics.GetDescriptors(sponsorable, diagnosticsIdPrefix)
        };
    }

    /// <summary>
    /// The sponsor account to check for sponsorships.
    /// </summary>
    public string Sponsorable { get; }

    /// <summary>
    /// The product that uses SponsorLink. Used in diagnostics to clarify the product requesting the sponsor link check.
    /// </summary>
    public string Product { get; }

    /// <summary>
    /// The supported diagnostics used by sponsorlink when reporting diagnostics.
    /// </summary>
    public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; set; } = ImmutableArray<DiagnosticDescriptor>.Empty;

    internal string? PackageId { get; private set; }
    internal string? Version { get; private set; }
    internal int PauseMin { get; private set; }
    internal int PauseMax { get; private set; }
    internal DateTime? InstallTime { get; set; }
    internal int? QuietDays { get; private set; }
    internal bool Transitive { get; private set; }
}
