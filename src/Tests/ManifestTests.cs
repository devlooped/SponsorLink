using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Devlooped.Sponsors;

namespace Devlooped.Tests;

public class ManifestTests
{
    [Fact]
    public void ValidateSponsorable()
    {
        var manifest = SponsorableManifest.Create(new Uri("https://foo.com"), [new Uri("https://github.com/sponsors/bar")], "ASDF1234");
        var jwt = manifest.ToJwt();

        // NOTE: sponsorable manifest doesn't have expiration date.
        var status = Manifest.Validate(jwt, manifest.SecurityKey, out var token, out var principal, false);

        Assert.Equal(Manifest.Status.Valid, status);
    }

    [Fact]
    public void ValidateWrongKey()
    {
        var manifest = SponsorableManifest.Create(new Uri("https://foo.com"), [new Uri("https://github.com/sponsors/bar")], "ASDF1234");
        var jwt = manifest.ToJwt();

        var key = RSA.Create();

        var status = Manifest.Validate(jwt, key, out var token, out var principal, false);

        Assert.Equal(Manifest.Status.Invalid, status);

        // We should still be a able to read the data, knowing it may have been tampered with.
        Assert.NotNull(principal);
        Assert.NotNull(token);
    }

    [Fact]
    public void ValidateExpiredSponsor()
    {
        var manifest = SponsorableManifest.Create(new Uri("https://foo.com"), [new Uri("https://github.com/sponsors/bar")], "ASDF1234");
        var sponsor = manifest.Sign([], expiration: TimeSpan.Zero);

        // Will be expired after this.
        Thread.Sleep(1000);

        var status = Manifest.Validate(sponsor, manifest.SecurityKey, out var token, out var principal, true);

        Assert.Equal(Manifest.Status.Expired, status);

        // We should still be a able to read the data, even if expired (but not tampered with).
        Assert.NotNull(principal);
        Assert.NotNull(token);
    }

    [Fact]
    public void ValidateUnknownFormat()
    {
        var manifest = SponsorableManifest.Create(new Uri("https://foo.com"), [new Uri("https://github.com/sponsors/bar")], "ASDF1234");

        var status = Manifest.Validate("asdfasdf", manifest.SecurityKey, out var token, out var principal, false);

        Assert.Equal(Manifest.Status.Unknown, status);

        // Nothing could be read at all.
        Assert.Null(principal);
        Assert.Null(token);
    }
}
