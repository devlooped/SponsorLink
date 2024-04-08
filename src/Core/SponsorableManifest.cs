using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

/// <summary>
/// The serializable manifest of a sponsorable user, as persisted 
/// in the .github/sponsorlink.jwt file.
/// </summary>
public class SponsorableManifest
{
    public static SponsorableManifest FromJwt(string jwt)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var issuer = token.Issuer;
        var audience = token.Audiences.FirstOrDefault() ?? throw new ArgumentException("Missing issuer claim", nameof(jwt));
        var pub = token.Claims.FirstOrDefault(c => c.Type == "pub")?.Value ?? throw new ArgumentException("Missing pub claim", nameof(jwt));
        var jwk = token.Claims.FirstOrDefault(c => c.Type == "sub_jwk")?.Value ?? throw new ArgumentException("Missing sub_jwk claim", nameof(jwt));

        var key = new JsonWebKeySet
        {
            Keys =
            {
                JsonWebKey.Create(jwk)
            }
        }.GetSigningKeys().First();

        return new SponsorableManifest
        {
            Issuer = issuer,
            Audience = audience,
            PublicKey = pub,
            SecurityKey = key,
        };
    }

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
    public required string PublicKey { get; set; }
    /// <summary>
    /// Public key in a format that can be used to verify JWT signatures.
    /// </summary>
    public required SecurityKey SecurityKey { get; set; }
}
