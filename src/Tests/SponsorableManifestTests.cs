using System.Security.Claims;
using System.Security.Cryptography;
using Devlooped.Sponsors;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Tests;

public class SponsorableManifestTests
{
    [Fact]
    public async Task CanCreateManifest()
    {
        var manifest = SponsorableManifest.Create(new Uri("https://foo.com"), [new Uri("https://github.com/sponsors/bar")], "ASDF1234");

        var jwt = manifest.ToJwt();

        // Ensures token is signed
        var result = await new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false
        }.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidateAudience = true,
            AudienceValidator = (audiences, token, parameters) => audiences.Any(x => x == "https://github.com/sponsors/bar"),
            ValidIssuer = manifest.Issuer,
            IssuerSigningKey = new RsaSecurityKey(((RsaSecurityKey)manifest.SecurityKey).Rsa.ExportParameters(false)),
        });

        var principal = new ClaimsPrincipal(result.ClaimsIdentity);

        Assert.Contains(principal.Claims, x => x.Type == "iss" && x.Issuer == "https://foo.com/");
        Assert.Contains(principal.Claims, x => x.Type == "aud" && x.Value == "https://github.com/sponsors/bar");
        Assert.Contains(principal.Claims, x => x.Type == "client_id" && x.Value == "ASDF1234");
        Assert.Contains(principal.Claims, x => x.Type == "sub_jwk");

        var jwk = JsonWebKey.Create(principal.Claims.First(x => x.Type == "sub_jwk").Value);
        Assert.False(jwk.HasPrivateKey);
    }

    [Fact]
    public void CanRoundtripManifest()
    {
        var rsa = RSA.Create(3072);
        var key = new RsaSecurityKey(rsa);
        var manifest = new SponsorableManifest(new Uri("https://foo.com"), [new Uri("https://github.com/sponsors/bar")], "ASDF1234", key);

        var jwt = manifest.ToJwt(new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        Assert.True(SponsorableManifest.TryRead(jwt, out var roundtripped, out _));

        Assert.Equal(manifest, roundtripped);
    }
}
