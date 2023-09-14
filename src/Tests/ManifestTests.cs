using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using static Devlooped.SponsorLink;

namespace Devlooped.Tests;

public class ManifestTests(ITestOutputHelper Output)
{
    [Fact]
    public void WhenReadingExpiredManifest_ThenCanStillCheckHashes()
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../test.key"), out var bytes);

        // Ensure we read the key
        Assert.NotEqual(0, bytes);

        var salt = Guid.NewGuid().ToString("N");

        var manifest = Manifest.Create(salt,
            "123", new[] { "foo@bar.com" }, new[] { "bar.com" }, new[] { "devlooped" },
            DateTime.Now.Subtract(TimeSpan.FromDays(30)));

        var jwt = manifest.Sign(rsa);

        var status = Manifest.TryRead(out var value, rsa, jwt, salt);
        Assert.Equal(ManifestStatus.Expired, status);
        Assert.NotNull(value);

        Assert.True(value.Contains("foo@bar.com", "devlooped"));
    }

    [Fact]
    public void EndToEnd()
    {
        var key = RSA.Create();
        key.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../test.key"), out _);

        var pub = RSA.Create();
        pub.ImportRSAPublicKey(File.ReadAllBytes(@"../../../test.pub"), out _);

        var salt = Guid.NewGuid().ToString("N");

        var manifest = Manifest.Create(salt, "1234",
            // user email(s)
            new[] { "foo@bar.com" },
            // org domains
            new[] { "bar.com", "baz.com" },
            // sponsorables
            new[] { "devlooped" });

        // Turn it into a signed manifest
        var signed = manifest.Sign(key);

        var validated = Manifest.Read(signed, salt, pub);

        // Direct sponsoring
        Assert.True(validated.Contains("foo@bar.com", "devlooped"));
        // Org sponsoring (via sponsoring domain)
        Assert.True(validated.Contains("baz@bar.com", "devlooped"));

        // Wrong sponsorable
        Assert.False(validated.Contains("foo@bar.com", "dotnet"));
        // Wrong email domain
        Assert.False(validated.Contains("foo@contoso.com", "devlooped"));
    }
}
