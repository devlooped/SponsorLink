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

public record SponsorshipCreated(string Sponsorable, string Sponsor, int Amount, DateOnly? ExpiresAt = null, string? Note = null);

public record SponsorshipChanged(string Sponsorable, string Sponsor, int Amount, string? Note = null);

public record SponsorshipCancelled(string Sponsorable, string Sponsor, string? Note = null);

public record SponsorshipExpired(string Sponsorable, string Sponsor, string? Note = null);