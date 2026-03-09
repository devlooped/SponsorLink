namespace Devlooped.Sponsors;

/// <summary>
/// Configuration options for X/Twitter release announcements.
/// Bound from the "X" configuration section.
/// </summary>
public class ReleaseAnnouncementOptions
{
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? AccessToken { get; set; }
    public string? AccessTokenSecret { get; set; }

    /// <summary>
    /// Whether the X/Twitter credentials are fully configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrEmpty(ApiKey) &&
        !string.IsNullOrEmpty(ApiSecret) &&
        !string.IsNullOrEmpty(AccessToken) &&
        !string.IsNullOrEmpty(AccessTokenSecret);
}
