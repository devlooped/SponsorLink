namespace Devlooped.Sponsors;

/// <summary>
/// Configured options for the sponsorable account set up with SponsorLink.
/// </summary>
/// <param name="Account">The sponsorable account using SponsorLink.</param>
/// <param name="PublicKey">The RSA public key used to verify SponsorLink manifests.</param>
public class SponsorLinkOptions
{
    /// <summary>
    /// Optional override of the account, which otherwise defaults to the owner 
    /// of the bearer key used to authenticate with the GitHub API.
    /// </summary>
    public string? Account { get; set; }

    /// <summary>
    /// Optional public key to verify SponsorLink manifests. Defaults 
    /// to the one in the JWT manifest of the sponsorable account .github repository.
    /// </summary>
    public string? PublicKey { get; set; }

    /// <summary>
    /// Optional private key to sign SponsorLink manifests.
    /// </summary>
    public string? PrivateKey { get; init; }
}
