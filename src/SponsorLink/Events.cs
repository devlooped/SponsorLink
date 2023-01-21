namespace Devlooped.SponsorLink;

/// <summary>
/// The given app was installed to the given <see cref="Account"/>.
/// </summary>
public record AppInstalled(string Account, string Login, AppKind Kind, string? Note = null);

/// <summary>
/// The given app was installed to the given <see cref="Account"/>.
/// </summary>
public record AppSuspended(string Account, string Login, AppKind Kind, string? Note = null);

/// <summary>
/// The given app was installed to the given <see cref="Account"/>.
/// </summary>
public record AppUnsuspended(string Account, string Login, AppKind Kind, string? Note = null);

/// <summary>
/// The given app was uninstalled from the given <see cref="Account"/>.
/// </summary>
public record AppUninstalled(string Account, string Login, AppKind Kind, string? Note = null);

/// <summary>
/// The given account has authorized a SponsorLink app.
/// </summary>
public record UserAuthorized(string Account, string Login, AppKind Kind, string? Note = null);

/// <summary>
/// User has authorized the app, but refreshing the subscription hasn't been successful. 
/// This can happen because there's a timing issue between app install and authorize. 
/// This event can also be triggered when users disable/enable the app to cause their 
/// sponsorship to be re-evaluated.
/// </summary>
public record UserRefreshPending(string Account, string Login, int Attempt, string? Note = null)
{
    /// <summary>
    /// Optional sponsorable to filter for the user sponsorship refresh.
    /// </summary>
    public string? Sponsorable { get; init; }
    /// <summary>
    /// Whether to unregister, instead of registering sponsorships.
    /// </summary>
    public bool Unregister { get; init; }
}

public record SponsorshipCreated(string Sponsorable, string Sponsor, int Amount, DateOnly? ExpiresAt = null, string? Note = null);

public record SponsorshipChanged(string Sponsorable, string Sponsor, int Amount, string? Note = null);

public record SponsorshipCancelled(string Sponsorable, string Sponsor, string? Note = null);

public record SponsorshipExpired(string Sponsorable, string Sponsor, string? Note = null);

public record SponsorRegistered(string Sponsorable, string Sponsor, string Email);

public record SponsorUnregistered(string Sponsorable, string Sponsor, string Email);