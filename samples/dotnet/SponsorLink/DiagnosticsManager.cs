﻿// <autogenerated />
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static Devlooped.Sponsors.SponsorLink;

namespace Devlooped.Sponsors;

/// <summary>
/// Manages diagnostics for the SponsorLink analyzer so that there are no duplicates 
/// when multiple projects share the same product name (i.e. ThisAssembly).
/// </summary>
class DiagnosticsManager
{
    static readonly Guid appDomainDiagnosticsKey = new(0x8d0e2670, 0xe6c4, 0x45c8, 0x81, 0xba, 0x5a, 0x36, 0x81, 0xd3, 0x65, 0x3e);

    public static Dictionary<SponsorStatus, DiagnosticDescriptor> KnownDescriptors { get; } = new()
    {
        // Requires:
        // <Constant Include="Funding.Product" Value="[PRODUCT_NAME]" />
        // <Constant Include="Funding.AnalyzerPrefix" Value="[PREFIX]" />
        { SponsorStatus.Unknown, CreateUnknown([.. Sponsorables.Keys], Funding.Product, Funding.Prefix) },
        { SponsorStatus.User, CreateSponsor([.. Sponsorables.Keys], Funding.Prefix) },
        { SponsorStatus.Contributor, CreateContributor([.. Sponsorables.Keys], Funding.Prefix) },
        // NOTE: organization is a special case of sponsor, but we report it as hidden since the user isn't directly involved.
        { SponsorStatus.Organization, CreateSponsor([.. Sponsorables.Keys], Funding.Prefix, hidden: true) },
        // NOTE: similar to organization, we don't show team membership in the IDE.
        { SponsorStatus.Team, CreateContributor([.. Sponsorables.Keys], Funding.Prefix, hidden: true) },
        { SponsorStatus.Expiring, CreateExpiring([.. Sponsorables.Keys], Funding.Prefix) },
        { SponsorStatus.Expired,  CreateExpired([.. Sponsorables.Keys], Funding.Prefix) },
    };

    /// <summary>
    /// Acceses the diagnostics dictionary for the current <see cref="AppDomain"/>.
    /// </summary>
    ConcurrentDictionary<string, Diagnostic> Diagnostics
        => AppDomainDictionary.Get<ConcurrentDictionary<string, Diagnostic>>(appDomainDiagnosticsKey.ToString());

    /// <summary>
    /// Gets the status of the given product based on a previously stored diagnostic. 
    /// To ensure the value is always set before returning, use <see cref="GetOrSetStatus"/>.
    /// This method is safe to use (and would get a non-null value) in analyzers that run after CompilationStartAction(see 
    /// https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md under Ordering of actions).
    /// </summary>
    /// <returns>Optional <see cref="SponsorStatus"/> that was reported, if any.</returns>
    /// <devdoc>
    /// The SponsorLinkAnalyzer.GetOrSetStatus uses diagnostic properties to store the 
    /// kind of diagnostic as a simple string instead of the enum. We do this so that 
    /// multiple analyzers or versions even across multiple products, which all would 
    /// have their own enum, can still share the same diagnostic kind.
    /// </devdoc>
    public SponsorStatus? GetStatus()
        => Diagnostics.TryGetValue(Funding.Product, out var diagnostic) ? GetStatus(diagnostic) : null;

    /// <summary>
    /// Gets the status of the <see cref="Funding.Product"/>, or sets it from 
    /// the given set of <paramref name="manifests"/> if not already set.
    /// </summary>
    public SponsorStatus GetOrSetStatus(ImmutableArray<AdditionalText> manifests, AnalyzerConfigOptionsProvider options)
        => GetOrSetStatus(() => manifests, () => options.GlobalOptions);

    /// <summary>
    /// Gets the status of the <see cref="Funding.Product"/>, or sets it from 
    /// the given analyzer <paramref name="options"/> if not already set.
    /// </summary>
    public SponsorStatus GetOrSetStatus(Func<AnalyzerOptions?> options)
        => GetOrSetStatus(() => options().GetSponsorAdditionalFiles(), () => options()?.AnalyzerConfigOptionsProvider.GlobalOptions);

    /// <summary>
    /// Attemps to remove a diagnostic for the given product.
    /// </summary>
    /// <param name="product">The product diagnostic that might have been pushed previously.</param>
    /// <returns>The removed diagnostic, or <see langword="null" /> if none was previously pushed.</returns>
    public Diagnostic? Pop(string product = Funding.Product)
    {
        if (Diagnostics.TryRemove(product, out var diagnostic) &&
            GetStatus(diagnostic) != SponsorStatus.Grace)
        {
            return diagnostic;
        }

        return null;
    }

    /// <summary>
    /// Pushes a diagnostic for the given product. 
    /// </summary>
    SponsorStatus Push(Diagnostic diagnostic, SponsorStatus status, string product = Funding.Product)
    {
        // We only expect to get one warning per sponsorable+product 
        // combination, and first one to set wins.
        Diagnostics.TryAdd(product, diagnostic);
        return status;
    }

    SponsorStatus GetOrSetStatus(Func<ImmutableArray<AdditionalText>> getAdditionalFiles, Func<AnalyzerConfigOptions?> getGlobalOptions)
    {
        if (GetStatus() is { } status)
            return status;

        if (!SponsorLink.TryRead(out var claims, getAdditionalFiles().Where(x => x.Path.EndsWith(".jwt")).Select(text =>
                (text.GetText()?.ToString() ?? "", Sponsorables[Path.GetFileNameWithoutExtension(text.Path)]))) ||
            claims.GetExpiration() is not DateTime exp)
        {
            var noGrace = getGlobalOptions() is { } globalOptions &&
               globalOptions.TryGetValue("build_property.SponsorLinkNoInstallGrace", out var value) &&
               bool.TryParse(value, out var skipCheck) && skipCheck;

            if (noGrace != true)
            {
                // Consider grace period if we can find the install time.
                var installed = getAdditionalFiles()
                    .Where(x => x.Path.EndsWith(".dll"))
                    .Select(x => File.GetLastWriteTime(x.Path))
                    .OrderByDescending(x => x)
                    .FirstOrDefault();

                if (installed != default && ((DateTime.Now - installed).TotalDays <= Funding.Grace))
                {
                    // report unknown, either unparsed manifest or one with no expiration (which we never emit).
                    return Push(Diagnostic.Create(KnownDescriptors[SponsorStatus.Unknown], null,
                        properties: ImmutableDictionary.Create<string, string?>().Add(nameof(SponsorStatus), nameof(SponsorStatus.Grace)),
                        Funding.Product, Sponsorables.Keys.Humanize(Resources.Or)),
                        SponsorStatus.Grace);
                }
            }

            // report unknown, either unparsed manifest or one with no expiration (which we never emit).
            return Push(Diagnostic.Create(KnownDescriptors[SponsorStatus.Unknown], null,
                properties: ImmutableDictionary.Create<string, string?>().Add(nameof(SponsorStatus), nameof(SponsorStatus.Unknown)),
                Funding.Product, Sponsorables.Keys.Humanize(Resources.Or)),
                SponsorStatus.Unknown);
        }
        else if (exp < DateTime.Now)
        {
            // report expired or expiring soon if still within the configured days of grace period
            if (exp.AddDays(Funding.Grace) < DateTime.Now)
            {
                // report expiring soon
                return Push(Diagnostic.Create(KnownDescriptors[SponsorStatus.Expiring], null,
                    properties: ImmutableDictionary.Create<string, string?>().Add(nameof(SponsorStatus), nameof(SponsorStatus.Expiring))),
                    SponsorStatus.Expiring);
            }
            else
            {
                // report expired
                return Push(Diagnostic.Create(KnownDescriptors[SponsorStatus.Expired], null,
                    properties: ImmutableDictionary.Create<string, string?>().Add(nameof(SponsorStatus), nameof(SponsorStatus.Expired))),
                    SponsorStatus.Expired);
            }
        }
        else
        {
            status =
                claims.IsInRole("team") ?
                SponsorStatus.Team :
                claims.IsInRole("user") ?
                SponsorStatus.User :
                claims.IsInRole("contrib") ?
                SponsorStatus.Contributor :
                claims.IsInRole("org") ?
                SponsorStatus.Organization : 
                SponsorStatus.Unknown;

            if (KnownDescriptors.TryGetValue(status, out var descriptor))
                return Push(Diagnostic.Create(descriptor, null,
                    properties: ImmutableDictionary.Create<string, string?>().Add(nameof(SponsorStatus), status.ToString()),
                    Funding.Product), status);

            return status;
        }
    }

    SponsorStatus? GetStatus(Diagnostic? diagnostic) => diagnostic?.Properties.TryGetValue(nameof(SponsorStatus), out var value) == true
        ? value switch
        {
            nameof(SponsorStatus.Grace) => SponsorStatus.Grace,
            nameof(SponsorStatus.Unknown) => SponsorStatus.Unknown,
            nameof(SponsorStatus.User) => SponsorStatus.User,
            nameof(SponsorStatus.Expiring) => SponsorStatus.Expiring,
            nameof(SponsorStatus.Expired) => SponsorStatus.Expired,
            _ => null,
        }
        : null;

    internal static DiagnosticDescriptor CreateUnknown(string[] sponsorable, string product, string prefix) => new(
        $"{prefix}100",
        Resources.Unknown_Title,
        Resources.Unknown_Message,
        "SponsorLink",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: string.Format(CultureInfo.CurrentCulture, Resources.Unknown_Description,
            string.Join(", ", sponsorable.Select(x => $"https://github.com/sponsors/{x}")),
            string.Join(" ", sponsorable)),
        helpLinkUri: "https://github.com/devlooped#sponsorlink",
        WellKnownDiagnosticTags.NotConfigurable, "CompilationEnd");

    internal static DiagnosticDescriptor CreateExpiring(string[] sponsorable, string prefix) => new(
         $"{prefix}101",
         Resources.Expiring_Title,
         Resources.Expiring_Message,
         "SponsorLink",
         DiagnosticSeverity.Warning,
         isEnabledByDefault: true,
         description: string.Format(CultureInfo.CurrentCulture, Resources.Expiring_Description, string.Join(" ", sponsorable)),
         helpLinkUri: "https://github.com/devlooped#autosync",
         "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable, "CompilationEnd");

    internal static DiagnosticDescriptor CreateExpired(string[] sponsorable, string prefix) => new(
         $"{prefix}102",
         Resources.Expired_Title,
         Resources.Expired_Message,
         "SponsorLink",
         DiagnosticSeverity.Warning,
         isEnabledByDefault: true,
         description: string.Format(CultureInfo.CurrentCulture, Resources.Expired_Description, string.Join(" ", sponsorable)),
         helpLinkUri: "https://github.com/devlooped#autosync",
         "DoesNotSupportF1Help", WellKnownDiagnosticTags.NotConfigurable, "CompilationEnd");

    internal static DiagnosticDescriptor CreateSponsor(string[] sponsorable, string prefix, bool hidden = false) => new(
            $"{prefix}105",
            Resources.Sponsor_Title,
            Resources.Sponsor_Message,
            "SponsorLink",
            hidden ? DiagnosticSeverity.Hidden : DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: Resources.Sponsor_Description,
            helpLinkUri: "https://github.com/devlooped#sponsorlink",
            "DoesNotSupportF1Help", "CompilationEnd");

    internal static DiagnosticDescriptor CreateContributor(string[] sponsorable, string prefix, bool hidden = false) => new(
            $"{prefix}106",
            Resources.Contributor_Title,
            Resources.Contributor_Message,
            "SponsorLink",
            hidden ? DiagnosticSeverity.Hidden : DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: Resources.Contributor_Description,
            helpLinkUri: "https://github.com/devlooped#sponsorlink",
            "DoesNotSupportF1Help", "CompilationEnd");
}
