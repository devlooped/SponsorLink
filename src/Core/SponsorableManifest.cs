using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

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

        return new SponsorableManifest(new Uri(issuer), new Uri(audience), clientId, key, pub);
    }

    /// <summary>
    /// Converts (and optionally signs) the manifest into a JWT. Never exports the private key.
    /// </summary>
    /// <param name="signing">Optional credentials when signing the resulting manifest. Defaults to the <see cref="SecurityKey"/> if it has a private key.</param>
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
    /// Sign the JWT claims with the provided RSA key.
    /// </summary>
    public string Sign(IEnumerable<Claim> claims, RSA rsa, TimeSpan? expiration = default)
        => Sign(claims, new RsaSecurityKey(rsa), expiration);

    public string Sign(IEnumerable<Claim> claims, RsaSecurityKey? key = default, TimeSpan? expiration = default)
    {
        var rsa = key ?? SecurityKey as RsaSecurityKey;
        if (rsa?.PrivateKeyStatus != PrivateKeyStatus.Exists)
            throw new NotSupportedException("No private key found to sign the manifest.");

        var signing = new SigningCredentials(rsa, SecurityAlgorithms.RsaSha256);

        var expirationDate = expiration != null ?
            DateTime.UtcNow.Add(expiration.Value) :
            // Expire the first day of the next month
            new DateTime(
                DateTime.UtcNow.AddMonths(1).Year, 
                DateTime.UtcNow.AddMonths(1).Month, 1,
                // Use current time so they don't expire all at the same time
                DateTime.UtcNow.Hour,
                DateTime.UtcNow.Minute,
                DateTime.UtcNow.Second,
                DateTime.UtcNow.Millisecond,
                DateTimeKind.Utc);

        var tokenClaims = claims.ToList();

        if (tokenClaims.Find(c => c.Type == "iss") is { } issuer)
        {
            if (issuer.Value != Issuer)
                throw new ArgumentException($"The received claims contain an incompatible issuer claim. If present, the claim must contain the value '{Issuer}' but was '{issuer.Value}'.");
        }
        else
        {
            tokenClaims.Insert(0, new("iss", Issuer));
        }

        if (tokenClaims.Find(c => c.Type == "aud") is { } audience)
        {
            if (audience.Value != Audience)
                throw new ArgumentException($"The received claims contain an incompatible audience claim. If present, the claim must contain the value '{Audience}' but was '{audience.Value}'.");
        }
        else
        {
            tokenClaims.Insert(1, new("aud", Audience));
        }

        // The other claims (client_id, pub, sub_jwk) claims are mostly for the SL manifest itself,
        // not for the user, so for now we don't add them. 

        // Don't allow mismatches of public manifest key and the one used to sign, to avoid 
        // weird run-time errors verifiying manifests that were signed with a different key.
        var pubKey = Convert.ToBase64String(rsa.Rsa.ExportRSAPublicKey());
        if (pubKey != PublicKey)
            throw new ArgumentException($"Cannot sign with a private key that does not match the manifest public key.");

        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: tokenClaims,
            expires: expirationDate,
            signingCredentials: signing
        ));

        return jwt;
    }

    public ClaimsPrincipal Validate(string jwt, out SecurityToken? token) => new JwtSecurityTokenHandler().ValidateToken(jwt, new TokenValidationParameters
    {
        RequireExpirationTime = true,
        // NOTE: setting this to false allows checking sponsorships even when the manifest is expired. 
        // This might be useful if package authors want to extend the manifest lifetime beyond the default 
        // 30 days and issue a warning on expiration, rather than an error and a forced sync.
        // If this is not set (or true), a SecurityTokenExpiredException exception will be thrown.
        ValidateLifetime = false,
        ValidAudience = Audience,
        ValidIssuer = Issuer,
        IssuerSigningKey = SecurityKey,
    }, out token);

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
