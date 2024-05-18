using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

/// <summary>
/// Validates manifests in JWT format.
/// </summary>
static partial class Manifest
{
    /// <summary>
    /// The resulting status from validation.
    /// </summary>
    public enum Status
    {
        /// <summary>
        /// The manifest couldn't be read at all.
        /// </summary>
        Unknown,
        /// <summary>
        /// The manifest was read and is valid (not expired and properly signed).
        /// </summary>
        Valid,
        /// <summary>
        /// The manifest was read but has expired.
        /// </summary>
        Expired,
        /// <summary>
        /// The manifest was read, but its signature is invalid.
        /// </summary>
        Invalid,
    }

    /// <summary>
    /// Validates the manifest signature and optional expiration.
    /// </summary>
    /// <param name="jwt">The JWT to validate.</param>
    /// <param name="key">The key to validate the manifest signature with.</param>
    /// <param name="token">Except when returning <see cref="Status.Unknown"/>, returns the security token read from the JWT, even if signature check failed.</param>
    /// <param name="principal">The associated claims, only when return value is not <see cref="Status.Unknown"/>.</param>
    /// <param name="requireExpiration">Whether to check for expiration.</param>
    /// <returns>The status of the validation.</returns>
    public static Status Validate(string jwt, RSA key, out SecurityToken? token, out ClaimsPrincipal? principal, bool validateExpiration)
        => Validate(jwt, new RsaSecurityKey(key), out token, out principal, validateExpiration);

    /// <summary>
    /// Validates the manifest signature and optional expiration.
    /// </summary>
    /// <param name="jwt">The JWT to validate.</param>
    /// <param name="key">The key to validate the manifest signature with.</param>
    /// <param name="token">Except when returning <see cref="Status.Unknown"/>, returns the security token read from the JWT, even if signature check failed.</param>
    /// <param name="principal">The associated claims, only when return value is not <see cref="Status.Unknown"/>.</param>
    /// <param name="requireExpiration">Whether to check for expiration.</param>
    /// <returns>The status of the validation.</returns>
    public static Status Validate(string jwt, SecurityKey key, out SecurityToken? token, out ClaimsPrincipal? principal, bool validateExpiration)
    {
        token = default;
        principal = default;
        var handler = new JwtSecurityTokenHandler();

        if (!handler.CanReadToken(jwt))
            return Status.Unknown;

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidateLifetime = false,
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
        };

        try
        {
            principal = handler.ValidateToken(jwt, validation, out token);
            if (validateExpiration && token.ValidTo == DateTime.MinValue)
                return Status.Invalid;

            // The sponsorable manifest does not have an expiration time.
            if (validateExpiration && token.ValidTo < DateTimeOffset.UtcNow)
                return Status.Expired;

            return Status.Valid;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            var jwtToken = handler.ReadJwtToken(jwt);
            token = jwtToken;
            principal = new ClaimsPrincipal(new ClaimsIdentity(jwtToken.Claims));
            return Status.Invalid;
        }
        catch (SecurityTokenException)
        {
            var jwtToken = handler.ReadJwtToken(jwt);
            token = jwtToken;
            principal = new ClaimsPrincipal(new ClaimsIdentity(jwtToken.Claims));
            return Status.Invalid;
        }
    }
}
