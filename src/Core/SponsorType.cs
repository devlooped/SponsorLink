namespace Devlooped.Sponsors;

/// <summary>
/// The types of sponsors.
/// </summary>
[Flags]
public enum SponsorTypes
{
    /// <summary>
    /// The user is not a sponsor.
    /// </summary>
    None = 0,
    /// <summary>
    /// The user is considered a sponsor because he is 
    /// a contributor to a project.
    /// </summary>
    Contributor = 1,
    /// <summary>
    /// The user is considered a sponsor because an 
    /// organization he belongs to is sponsoring.
    /// </summary>
    Organization = 2,
    /// <summary>
    /// The user is directly sponsoring.
    /// </summary>
    User = 4,
    /// <summary>
    /// The user is the sponsorable account or member of the organization.
    /// </summary>
    Team = 8,
}
