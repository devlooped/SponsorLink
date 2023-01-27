namespace Devlooped;

/// <summary>
/// The kind of SponsorLink diagnostic being reported.
/// </summary>
public enum DiagnosticKind
{ 
    /// <summary>
    /// The SponsorLink GitHub is not installed on the user's personal account.
    /// </summary>
    AppNotInstalled,
    /// <summary>
    /// The user is not sponsoring the given sponsor account.
    /// </summary>
    UserNotSponsoring,
    /// <summary>
    /// The user has the SponsorLink GitHub app installed and is sponsoring the given sponsor account.
    /// </summary>
    Thanks
}
