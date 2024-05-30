using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
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

    [Fact]
    public void TryRead()
    {
        var fooSponsorable = SponsorableManifest.Create(new Uri("https://foo.com"), [new Uri("https://github.com/sponsors/foo")], "ASDF1234");
        var barSponsorable = SponsorableManifest.Create(new Uri("https://bar.com"), [new Uri("https://github.com/sponsors/bar")], "GHJK5678");
        // Org sponsor and member of team
        var fooSponsor = fooSponsorable.Sign([new("sub", "kzu"), new("email", "me@foo.com"), new("roles", "org"), new("roles", "team")], expiration: TimeSpan.FromDays(30));
        // Org + personal sponsor
        var barSponsor = barSponsorable.Sign([new("sub", "kzu"), new("email", "me@bar.com"), new("roles", "org"), new("roles", "user")], expiration: TimeSpan.FromDays(30));

        Assert.True(Manifest.TryRead(out var principal, (fooSponsor, fooSponsorable.PublicKey), (barSponsor, barSponsorable.PublicKey)));

        // Can check role across both JWTs
        Assert.True(principal.IsInRole("org"));
        Assert.True(principal.IsInRole("team"));
        Assert.True(principal.IsInRole("user"));

        Assert.True(principal.HasClaim("sub", "kzu"));
        Assert.True(principal.HasClaim("email", "me@foo.com"));
        Assert.True(principal.HasClaim("email", "me@bar.com"));
    }

    [LocalFact]
    public void ValidateCachedManifest()
    {
        var path = Environment.ExpandEnvironmentVariables("%userprofile%\\.sponsorlink\\github\\devlooped.jwt");
        if (!File.Exists(path))
            return;

        var jwt = File.ReadAllText(path);

        var status = Manifest.Validate(jwt, "MIIBigKCAYEA5inhv8QymaDBOihNi1eY+6+hcIB5qSONFZxbxxXAyOtxAdjFCPM+94gIZqM9CDrX3pyg1lTJfml/a/FZSU9dB1ii5mSX/mNHBFXn1/l/gi1ErdbkIF5YbW6oxWFxf3G5mwVXwnPfxHTyQdmWQ3YJR+A3EB4kaFwLqA6Ha5lb2ObGpMTQJNakD4oTAGDhqHMGhu6PupGq5ie4qZcQ7N8ANw8xH7nicTkbqEhQABHWOTmLBWq5f5F6RYGF8P7cl0IWl/w4YcIZkGm2vX2fi26F9F60cU1v13GZEVDTXpJ9kzvYeM9sYk6fWaoyY2jhE51qbv0B0u6hScZiLREtm3n7ClJbIGXhkUppFS2JlNaX3rgQ6t+4LK8gUTyLt3zDs2H8OZyCwlCpfmGmdsUMkm1xX6t2r+95U3zywynxoWZfjBCJf41leM9OMKYwNWZ6LQMyo83HWw1PBIrX4ZLClFwqBcSYsXDyT8/ZLd1cdYmPfmtllIXxZhLClwT5qbCWv73VAgMBAAE=", out var token, out var principal, false);

        Assert.Equal(Manifest.Status.Valid, status);
    }
}
