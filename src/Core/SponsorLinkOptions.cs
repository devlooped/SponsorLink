using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Devlooped.Sponsors;

/// <summary>
/// Configured options for the sponsorable account set up with SponsorLink.
/// </summary>
public class SponsorLinkOptions
{
    /// <summary>
    /// Optional override of the account, which otherwise defaults to the owner 
    /// of the bearer key used to authenticate with the GitHub API.
    /// </summary>
    [Required]
    public string? Account { get; set; }

    /// <summary>
    /// Optional private key to sign SponsorLink manifests.
    /// </summary>
    public string? PrivateKey { get; init; }

    /// <summary>
    /// Optional override of the default branch to fetch sponsorable manifest from.
    /// </summary>
    public string? ManifestBranch { get; set; }

    /// <summary>
    /// Timespan for the expiration of the sponsorable manifest, in a format compatible with <see cref="TimeSpan.Parse(string)"/>.
    /// </summary>
    public string ManifestExpiration { get; init; } = "01:00:00";

    /// <summary>
    /// Timespan for the expiration of the badge cache, in a format compatible with <see cref="TimeSpan.Parse(string)"/>.
    /// </summary>
    public string BadgeExpiration { get; init; } = "00:05:00";

    /// <summary>
    /// Optional Azure Log Analytics workspace ID to produce usage badges from the /badge endpoint.
    /// </summary>
    /// <remarks>
    /// Example badge usage: https://img.shields.io/endpoint?color=blue&url=https://sponsorlink.devlooped.com/badge?user
    /// </remarks>
    public string? LogAnalytics { get; init; }
}
