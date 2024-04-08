using System.Collections;
using System.Security.Cryptography;
using Devlooped.Sponsors;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Tests;

public class SponsorableManifestTests
{
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
