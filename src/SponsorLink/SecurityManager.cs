using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JWT.Algorithms;
using JWT.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devlooped.SponsorLink;

[Service]
public record SecurityManager
{
    readonly RSA sponsorKey;
    readonly RSA sponsorableKey;

    public IConfiguration Configuration { get; }

    public SecurityManager(IConfiguration configuration)
    {
        Configuration = configuration;

        // TODO: move both to app settings/secret? Once we replace the private keys, 
        // we can remove the files from here. Not critical to share this because 
        // they can't be used to set up your own app private key, you can only generate
        // a new private key in the the app settings UI.

        sponsorKey = RSA.Create();
        sponsorKey.ImportFromPem(configuration["GitHub:Sponsor:PrivateKey"]);

        sponsorableKey = RSA.Create();
        sponsorableKey.ImportFromPem(configuration["GitHub:Sponsorable:PrivateKey"]);
    }

    /// <summary>
    /// Creates the OAuth authorization payload to exchange for the given code.
    /// </summary>
    public string CreateAuthorization(AppKind kind, string code)
    {
        var prefix = $"GitHub:{kind}:";
        var client_id = Configuration[prefix + "ClientId"] ?? throw new InvalidOperationException($"Missing {prefix}ClientId");
        var client_secret = Configuration[prefix + "ClientSecret"] ?? throw new InvalidOperationException($"Missing {prefix}ClientSecret");
        var redirect_uri = Configuration[prefix + "RedirectUri"] ?? throw new InvalidOperationException($"Missing {prefix}RedirectUri");

        return JsonSerializer.Serialize(new { client_id, client_secret, code, redirect_uri });
    }

    /// <summary>
    /// Issues a JWT token for the given app kind.
    /// </summary>
    public string IssueToken(AppKind kind)
    {
        var app_id = Configuration[$"GitHub:{kind}:AppId"] ?? throw new InvalidOperationException($"Missing GitHub:{kind}:AppId");
        var key = kind switch
        {
            AppKind.Sponsorable => sponsorableKey,
            AppKind.Sponsor => sponsorKey,
            _ => throw new ArgumentException($"Unknown app kind {kind}.")
        };

        var jwt = JwtBuilder.Create()
                  .WithAlgorithm(new RS256Algorithm(key, key))
                  .Issuer(app_id)
                  .AddClaim("iat", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds())
                  .AddClaim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds())
                  .Encode();

        return jwt;
    }

    public static bool VerifySignature(string payload, string? secret, string? signature)
    {
        if (secret == null)
        {
            if (signature == null)
                // If neither secret nor signature were received, we succeed (no signing in place at all)
                return true;
            else
                // If we got a signature but have no secret to verify, that's a misconfiguration 
                return false;
        }

        // If we have a secret but got no signature, request didn't came from where we expected
        if (signature == null)
            return false;

        var hash = "sha256=" + string.Concat(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(payload))
            .Select(c => c.ToString("x2")));

        return hash == signature;
    }
}
