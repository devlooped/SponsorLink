using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Devlooped.Sponsors;

/// <summary>
/// The serializable manifest of a sponsorable user, as persisted 
/// in the .github/sponsorlink.jwt file.
/// </summary>
public class SponsorableManifest(Uri issuer, Uri audience, string clientId, SecurityKey publicKey, string publicRsaKey)
{
    int? hashcode;

    /// <summary>
    /// Creates a new manifest with a new RSA key pair.
    /// </summary>
    public static SponsorableManifest Create(Uri issuer, Uri audience, string clientId)
    {
        var rsa = RSA.Create(3072);
        var pub = Convert.ToBase64String(rsa.ExportRSAPublicKey());

        return new SponsorableManifest(issuer, audience, clientId, new RsaSecurityKey(rsa), pub);
    }

    /// <summary>
    /// Parses a JWT into a <see cref="SponsorableManifest"/>.
    /// </summary>
    /// <param name="jwt">The JWT containing the sponsorable information.</param>
    /// <returns>A validated manifest.</returns>
    /// <exception cref="ArgumentException">A required claim was not found in the JWT.</exception>
    public static SponsorableManifest FromJwt(string jwt)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var issuer = token.Issuer;
        var audience = token.Audiences.FirstOrDefault() ?? throw new ArgumentException("Missing 'issuer' claim", nameof(jwt));
        var clientId = token.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value ?? throw new ArgumentException("Missing 'client_id' claim", nameof(jwt));
        var pub = token.Claims.FirstOrDefault(c => c.Type == "pub")?.Value ?? throw new ArgumentException("Missing 'pub' claim", nameof(jwt));
        var jwk = token.Claims.FirstOrDefault(c => c.Type == "sub_jwk")?.Value ?? throw new ArgumentException("Missing 'sub_jwk' claim", nameof(jwt));
        var key = new JsonWebKeySet { Keys = { JsonWebKey.Create(jwk) } }.GetSigningKeys().First();

        return new SponsorableManifest(new Uri(issuer), new Uri(audience), clientId,key, pub);
    }

    /// <summary>
    /// Converts (and optionally signs) the manifest into a JWT.
    /// </summary>
    /// <param name="signing">Optional credentials when signing the resulting manifest.</param>
    /// <returns>The JWT manifest.</returns>
    public string ToJwt(SigningCredentials? signing = default)
    {
        var jwk = JsonWebKeyConverter.ConvertFromSecurityKey(SecurityKey);

        // Automatically sign if the manifest was created with a private key
        if (SecurityKey is RsaSecurityKey rsa && rsa.PrivateKeyStatus == PrivateKeyStatus.Exists)
        {
            signing ??= new SigningCredentials(rsa, SecurityAlgorithms.RsaSha256);

            // Ensure we never serialize the private key
            jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.Rsa.ExportParameters(false)));
        }

        var token = new JwtSecurityToken(
            claims:
            [
                new("iss", Issuer),
                new("aud", Audience),
                new("client_id", clientId),
                // non-standard claim containing the base64-encoded public key
                new("pub", publicRsaKey),
                // standard claim, serialized as a JSON string, not an encoded JSON object
                new("sub_jwk", JsonSerializer.Serialize(jwk, JsonOptions.JsonWebKey), JsonClaimValueTypes.Json),
            ],
            signingCredentials: signing);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// The web endpoint that issues signed JWT to authenticated users.
    /// </summary>
    /// <remarks>
    /// See https://www.rfc-editor.org/rfc/rfc7519.html#section-4.1.1
    /// </remarks>
    public string Issuer => issuer.AbsoluteUri;
    /// <summary>
    /// The audience for the JWT, which is the sponsorable account.
    /// </summary>
    /// <remarks>
    /// See https://www.rfc-editor.org/rfc/rfc7519.html#section-4.1.3
    /// </remarks>
    public string Audience => audience.AbsoluteUri;
    /// <summary>
    /// The OAuth client ID (i.e. GitHub OAuth App ID) that is used to 
    /// authenticate the user.
    /// </summary>
    /// <remarks>
    /// See https://www.rfc-editor.org/rfc/rfc8693.html#name-client_id-client-identifier
    /// </remarks>
    public string ClientId => clientId;
    /// <summary>
    /// Public key that can be used to verify JWT signatures.
    /// </summary>
    public string PublicKey => publicRsaKey;
    /// <summary>
    /// Public key in a format that can be used to verify JWT signatures.
    /// </summary>
    public SecurityKey SecurityKey => publicKey;

    /// <inheritdoc/>
    public override int GetHashCode() => hashcode ??= HashCode.Combine(Issuer, Audience, ClientId, PublicKey);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SponsorableManifest other && GetHashCode() == other.GetHashCode();
}
