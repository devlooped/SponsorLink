namespace Devlooped.Sponsors;

/// <summary>
/// The type of sponsor the user is.
/// </summary>
public enum SponsorType
{
    /// <summary>
    /// The user is not a sponsor.
    /// </summary>
    None,
    /// <summary>
    /// The user is considered a sponsor because it is 
    /// a contributor to a project.
    /// </summary>
    Contributor,
    /// <summary>
    /// The user is considered a sponsor because an 
    /// organization it belongs to is sponsoring.
    /// </summary>
    Organization,
    /// <summary>
    /// The user is directly sponsoring.
    /// </summary>
    User,
}
