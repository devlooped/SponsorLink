using System.Security.Cryptography;
using JWT.Algorithms;
using JWT.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Devlooped.SponsorLink;

[Service]
public record SecurityManager(IConfiguration Configuration)
{
    static readonly RSA sponsorKey;
    static readonly RSA sponsorableKey;

    static SecurityManager()
    {
        sponsorKey = RSA.Create();
        sponsorKey.ImportFromPem(ThisAssembly.Resources.sponsorlink_sponsor.Text);

        sponsorableKey = RSA.Create();
        sponsorableKey.ImportFromPem(ThisAssembly.Resources.sponsorlink_sponsorable.Text);
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
}
