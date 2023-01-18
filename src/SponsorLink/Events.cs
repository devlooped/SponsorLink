namespace Devlooped.SponsorLink;

/// <summary>
/// The given app was installed to the given <see cref="Account"/>.
/// </summary>
public record AppInstalled(string Account, AppKind Kind, string? Note = null);

/// <summary>
/// The given app was installed to the given <see cref="Account"/>.
/// </summary>
public record AppSuspended(string Account, AppKind Kind, string? Note = null);

/// <summary>
/// The given app was installed to the given <see cref="Account"/>.
/// </summary>
public record AppUnsuspended(string Account, AppKind Kind, string? Note = null);

/// <summary>
/// The given app was uninstalled from the given <see cref="Account"/>.
/// </summary>
public record AppUninstalled(string Account, AppKind Kind, string? Note = null);

//- Admin.Installed(Id, Note)       
//- Admin.Removed(Id, Note)
//- User.Authorized(Id, AccessToken, Note)
//- User.Updated(Id, Emails[], Note)
//- Sponsorship.Created(SponsorableId, SponsorId, Amount, ExpiresAt, Note) // [sponsor] > [sponsorable] : $1
//- Sponsorship.Changed(SponsorableId, SponsorId, Amount, Note)            // [sponsor] > [sponsorable] : $1 > $2
//- Sponsorship.Cancelled(SponsorableId, SponsorId, CancelAt, Note)        // [sponsor] x [sponsorable] on [date]
//- Sponsorship.Expired(SponsorableId, SponsorId, Note)                    // [sponsor] x [sponsorable]  > cancelled+date expiration

