using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

public class ManifestTests(ITestOutputHelper Output)
{
    [Fact]
    public void Init()
    {
        var issuer = "https://sponsorlink.us.auth0.com/";
        var sync = "https://sponsorlink.devlooped.com/sync";
        var pub = Convert.ToBase64String(File.ReadAllBytes(@"../../../test.pub"));

        var token = new JwtSecurityToken(
            issuer: issuer,
            claims: new[]
            {
                new Claim("pub", pub),
                new Claim("sync", sync),
            });

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        Output.WriteLine(jwt);
        Output.WriteLine(token.ToString());
        //Output.WriteLine(token.UnsafeToString());

        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../test.key"), out var bytes);

        var signing = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var signed = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer,
            claims: new[]
            {
                new Claim("pub", pub),
                new Claim("sync", sync),
            },
            signingCredentials: signing));

        Output.WriteLine(signed);
        Output.WriteLine("Public Key:");
        Output.WriteLine(Convert.ToBase64String(File.ReadAllBytes(@"../../../test.pub")));
        Output.WriteLine("Private Key:");
        Output.WriteLine(Convert.ToBase64String(File.ReadAllBytes(@"../../../test.key")));

        Output.WriteLine("Public JWK:");
        Output.WriteLine(
            JsonSerializer.Serialize(
                JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false))),
                JsonOptions.Default));

        Output.WriteLine("Private JWK:");
        Output.WriteLine(
            JsonSerializer.Serialize(
                JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(true))),
                JsonOptions.Default));
    }

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
