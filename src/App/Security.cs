using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.SponsorLink;

static class Security
{
    static readonly IConfigurationManager<OpenIdConnectConfiguration> configuration;

    static readonly string Issuer = "https://sponsorlink.us.auth0.com/";
    static readonly string Audience = "https://sponsorlink.devlooped.com";

    static Security()
    {
        var documentRetriever = new HttpDocumentRetriever { RequireHttps = true };

        configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{Issuer}.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            documentRetriever
        );
    }

    public static async Task<ClaimsPrincipal?> ValidateTokenAsync(AuthenticationHeaderValue value)
    {
        if (value?.Scheme != "Bearer")
            return null;

        var config = await configuration.GetConfigurationAsync(CancellationToken.None);

        var validationParameter = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidAudience = Audience,
            ValidateAudience = true,
            ValidIssuer = Issuer,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            IssuerSigningKeys = config.SigningKeys
        };

        ClaimsPrincipal? result = default;
        var tries = 0;

        while (result == null && tries <= 1)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                return handler.ValidateToken(value.Parameter, validationParameter, out var _);
            }
            catch (SecurityTokenSignatureKeyNotFoundException)
            {
                // This exception is thrown if the signature key of the JWT could not be found.
                // This could be the case when the issuer changed its signing keys, so we trigger a 
                // refresh and retry validation.
                configuration.RequestRefresh();
                tries++;
            }
            catch (SecurityTokenException)
            {
                return null;
            }
        }

        return result;
    }
}
