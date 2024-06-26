﻿namespace Devlooped.Sponsors;

/// <summary>
/// Configured options for the sponsorable account set up with SponsorLink.
/// </summary>
public class SponsorLinkOptions
{
    /// <summary>
    /// Optional override of the account, which otherwise defaults to the owner 
    /// of the bearer key used to authenticate with the GitHub API.
    /// </summary>
    public string? Account { get; set; }

    /// <summary>
    /// Optional private key to sign SponsorLink manifests.
    /// </summary>
    public string? PrivateKey { get; init; }

    /// <summary>
    /// Timespan for the expiration of the sponsorable manifest, in a format compatible with <see cref="TimeSpan.Parse(string)"/>.
    /// </summary>
    public string? ManifestExpiration { get; init; }

    /// <summary>
    /// Timespan for the expiration of the badge cache, in a format compatible with <see cref="TimeSpan.Parse(string)"/>.
    /// </summary>
    public string? BadgeExpiration { get; init; }
}
