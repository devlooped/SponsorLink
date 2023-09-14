using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Spectre.Console;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

/// <summary>
/// Manages session token, authentication and installation ininitialization.
/// </summary>
public static class Session
{
    static readonly SHA256 sha = SHA256.Create();
    const string Issuer = "https://sponsorlink.us.auth0.com/";
    const string Audience = "https://sponsorlink.devlooped.com";

    //static readonly string AccessTokenFile = Path.Combine(
    //    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
    //    ".sponsorlink.jwt");

    static readonly IConfigurationManager<OpenIdConnectConfiguration> configuration;

    static Session()
    {
        configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{Issuer}.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });

        if (Variables.InstallationId is not string installation)
        {
            installation = Guid.NewGuid().ToString("N");
            Variables.InstallationId = installation;
        }

        InstallationId = installation;
    }

    /// <summary>
    /// Gets a unique identifier for this installation, which can be used for salting 
    /// hashes to preserve privacy.
    /// </summary>
    public static string InstallationId { get; private set; }

    /// <summary>
    /// Authenticates with SponsorLink and returns the user claims.
    /// </summary>
    public static async Task<ClaimsPrincipal?> AuthenticateAsync()
    {
        // We cache the token in an environment variable to avoid having to re-authenticate
        // unless the token is expired or invalid.
        if (Variables.AccessToken is string token &&
            await ValidateTokenAsync(token) is ClaimsPrincipal principal)
        {
            return principal;
        }

        // TODO: ask to open browser?
        if (!AnsiConsole.Confirm(ThisAssembly.Strings.Session.OpenBrowser))
        {
            return null;
        }

        var client = new AuthenticationApiClient(new Uri(Issuer));
        var verifier = Guid.NewGuid().ToString("N");
        var challenge = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(verifier)));

        var uri = client.BuildAuthorizationUrl()
            .WithAudience("https://sponsorlink.devlooped.com")
            .WithClient("ZUMhc9T4TdJtTsjGaKwEnVZALgpw0fF9")
            .WithScopes("openid", "profile")
            .WithResponseType(AuthorizationResponseType.Code)
            .WithNonce(challenge)
            .WithRedirectUrl("http://localhost:4242")
            .Build();

        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:4242/");
        listener.Start();

        var getCode = Task.Run(() =>
        {
            var context = listener.GetContext();
            var code = context.Request.QueryString["code"];

            context.Response.StatusCode = 200;
            context.Response.Redirect("https://devlooped.com?cli");
            context.Response.Close();

            return code;
        });

        System.Diagnostics.Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });

        var code = await getCode;
        listener.Stop();

        // Exchange the code for a token
        var response = await client.GetTokenAsync(new AuthorizationCodePkceTokenRequest
        {
            ClientId = "ZUMhc9T4TdJtTsjGaKwEnVZALgpw0fF9",
            Code = code,
            CodeVerifier = verifier,
            RedirectUri = "http://localhost:4242",
        });

        token = response.AccessToken;

        if (await ValidateTokenAsync(token) is ClaimsPrincipal validated)
        {
            Variables.AccessToken = token;
            return validated;
        }

        AnsiConsole.MarkupLine("[red]x[/] Could not determine authenticated user id.");
        return default;
    }

    static async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
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
                return handler.ValidateToken(token, validationParameter, out var _);
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
