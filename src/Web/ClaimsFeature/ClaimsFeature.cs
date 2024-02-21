namespace System.Security.Claims;

/// <summary>
/// Holds the authenticated user principal for the request along with the
/// access token they used.
/// </summary>
public class ClaimsFeature(ClaimsPrincipal principal, string? accessToken = default)
{
    /// <summary>
    /// The authenticated user principal for the request.
    /// </summary>
    public ClaimsPrincipal Principal => principal;

    /// <summary>
    /// The access token that was used for this request. 
    /// </summary>
    public string? AccessToken => accessToken;
}