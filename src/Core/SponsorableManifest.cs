using SharpYaml.Serialization;

namespace Devlooped.Sponsors;

/// <summary>
/// The serializable manifest of a sponsorable user, as persisted 
/// in the .github/sponsorlink.yml file.
/// </summary>
public class SponsorableManifest
{
    /// <summary>
    /// The web endpoint that issues signed JWT to authenticated users.
    /// </summary>
    public required string Issuer { get; set; }
    /// <summary>
    /// The audience for the JWT, which is the sponsorable account.
    /// </summary>
    public required string Audience { get; set; }
    /// <summary>
    /// The type of account of <see cref="Audience"/>.
    /// </summary>
    public AccountType AccountType { get; set; }
    /// <summary>
    /// Public key that can be used to verify JWT signatures.
    /// </summary>
    [YamlMember("pub")]
    public required string PublicKey { get; set; }
}
