namespace Devlooped.Sponsors;

/// <summary>
/// The type of sponsor.
/// </summary>
public enum SponsorType
{
    /// <summary>
    /// The user is not a sponsor.
    /// </summary>
    None,
    /// <summary>
    /// The user is considered a sponsor because he is 
    /// a contributor to a project.
    /// </summary>
    Contributor,
    /// <summary>
    /// The user is considered a sponsor because an 
    /// organization he belongs to is sponsoring.
    /// </summary>
    Organization,
    /// <summary>
    /// The user is directly sponsoring.
    /// </summary>
    User,
    /// <summary>
    /// The user is the sponsorable account or member of the organization.
    /// </summary>
    Member,
}
