using System.Collections;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Sockets;
using System.Security.Cryptography;
using Devlooped.Sponsors;
using Microsoft.IdentityModel.Tokens;
using SharpYaml.Tokens;

namespace Devlooped.Tests;

public class SponsorableManifestTests
{
    [Fact]
    public void CanCreateManifest()
    {
        var manifest = SponsorableManifest.Create(new Uri("https://foo.com"), new Uri("https://bar.com"), "ASDF1234");

        var jwt = manifest.ToJwt();

        // Ensures token is signed
        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidAudience = manifest.Audience,
            ValidIssuer = manifest.Issuer,
            IssuerSigningKey = new RsaSecurityKey(((RsaSecurityKey)manifest.SecurityKey).Rsa.ExportParameters(false)),
        }, out var secToken);

        Assert.Contains(principal.Claims, x => x.Type == "iss" && x.Issuer == "https://foo.com/");
        Assert.Contains(principal.Claims, x => x.Type == "aud" && x.Value == "https://bar.com/");
        Assert.Contains(principal.Claims, x => x.Type == "client_id" && x.Value == "ASDF1234");
        Assert.Contains(principal.Claims, x => x.Type == "pub" && x.Value == manifest.PublicKey);
        Assert.Contains(principal.Claims, x => x.Type == "sub_jwk");

        var jwk = JsonWebKey.Create(principal.Claims.First(x => x.Type == "sub_jwk").Value);
        Assert.False(jwk.HasPrivateKey);
    }

    [Fact]
    public void CanRoundtripManifest()
    {
        var rsa = RSA.Create(3072);
        var key = new RsaSecurityKey(rsa);
        var pub = Convert.ToBase64String(rsa.ExportRSAPublicKey());

        var manifest = new SponsorableManifest(new Uri("https://foo.com"), new Uri("https://bar.com"), "ASDF1234", key, pub);

        var jwt = manifest.ToJwt(new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        var roundtripped = SponsorableManifest.FromJwt(jwt);

        Assert.Equal(manifest, roundtripped);
    }
}
