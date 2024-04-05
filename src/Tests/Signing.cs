using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

public class Signing(ITestOutputHelper Output)
{
    static Signing()
    {
        IdentityModelEventSource.ShowPII = true;
        IdentityModelEventSource.LogCompleteSecurityArtifact = true;
    }

    // NOTE: if you want to locally regenerate the keys, uncomment the following line
    // NOTE: if you want to run locally the SL Functions App, you need to set the public 
    // key as Base64 encoded string in the SPONSORLINK_KEY environment variable
    [Fact]
    public void CreateKeyPair()
    {
        // Generate key pair
        RSA rsa = RSA.Create(2048);

        File.WriteAllBytes(@"../../../signing.pub", rsa.ExportRSAPublicKey());
        File.WriteAllText(@"../../../signing.txt", Convert.ToBase64String(rsa.ExportRSAPublicKey()), Encoding.UTF8);
        File.WriteAllBytes(@"../../../signing.key", rsa.ExportRSAPrivateKey());

        File.WriteAllBytes(@"../../../signing.pub2", RSA.Create(2048).ExportRSAPublicKey());
    }

    [Fact]
    public void WritePublicKey() => Output.WriteLine(Convert.ToBase64String(PublicKey.ExportRSAPublicKey()));

    [Fact]
    public void SignFile()
    {
        var priv = RSA.Create();
        priv.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../signing.key"), out _);

        byte[] data = Encoding.UTF8.GetBytes("Hello, world!");

        // Sign data
        byte[] signature = priv.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var pub = RSA.Create();
        pub.ImportRSAPublicKey(File.ReadAllBytes(@"../../../signing.pub"), out _);

        // Verify signature using public key
        Assert.True(pub.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void JwtSigning()
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../signing.key"), out _);

        var securityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new Claim("email", "foo@bar.com"),
            new Claim("email", "bar@baz.com"),
            new Claim("sponsor", "asdfasdf"),
            new Claim("sponsor", "zxcvzxcv")
        };

        var token = new JwtSecurityToken(
            issuer: "https://sponsorlink.devlooped.com/",
            audience: "devlooped",
            claims: claims,
            signingCredentials: signingCredentials);

        // Serialize the token and return as a string
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        var pub = RSA.Create();
        pub.ImportRSAPublicKey(File.ReadAllBytes(@"../../../signing.pub"), out _);

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidAudience = token.Audiences.First(),
            ValidIssuer = token.Issuer,
            IssuerSigningKey = new RsaSecurityKey(pub)
        };

        // Validation succeeds
        new JwtSecurityTokenHandler().ValidateToken(jwt, validation, out var securityToken);
    }

    [Fact]
    public void JwtSponsorableManifest()
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../signing.key"), out _);

        var securityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new Claim("pub", File.ReadAllText(@"../../../signing.txt", Encoding.UTF8)),
        };

        var token = new JwtSecurityToken(
            issuer: "https://sponsorlink.devlooped.com/",
            audience: "devlooped",
            claims: claims,
            signingCredentials: signingCredentials);

        // Serialize the token and return as a string
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        // Your code block where you're reading and validating JWT with public key

        // Read the public key from the manifest itself before validating
        token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        var pubvalue = token.Claims.First(c => c.Type == "pub").Value;

        var pub = RSA.Create();
        pub.ImportRSAPublicKey(Convert.FromBase64String(pubvalue), out _);

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidAudience = token.Audiences.First(),
            ValidIssuer = token.Issuer,
            IssuerSigningKey = new RsaSecurityKey(pub),
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, validation, out var securityToken);

        Assert.Contains("pub", principal.Claims.Select(c => c.Type));
        Assert.Contains("iss", principal.Claims.Select(c => c.Type));
        Assert.Contains("aud", principal.Claims.Select(c => c.Type));

        // Now you can use the principal to extract the claims and do whatever you need to do
        foreach (var claim in principal.Claims)
        {
            Output.WriteLine($"{claim.Type}: {claim.Value}");
        }
    }

    [Fact]
    public void JwtWrongPublicKey()
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../signing.key"), out _);

        var securityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new Claim("pub", File.ReadAllText(@"../../../signing.txt", Encoding.UTF8)),
        };

        var token = new JwtSecurityToken(
            issuer: "https://sponsorlink.devlooped.com/",
            audience: "devlooped",
            claims: claims,
            signingCredentials: signingCredentials);

        // Serialize the token and return as a string
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        // Your code block where you're reading and validating JWT with public key

        // Read the public key from the manifest itself before validating
        token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        var pubvalue = token.Claims.First(c => c.Type == "pub").Value;

        var pub = RSA.Create();
        // Import a different one from the one used for signing, simulates a 
        // bad actor using MITM to replace the manifest and signing it with another key
        pub.ImportRSAPublicKey(File.ReadAllBytes(@"../../../signing.pub2"), out _);

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidAudience = "devlooped",
            ValidIssuer = "https://sponsorlink.devlooped.com/",
            IssuerSigningKey = new RsaSecurityKey(pub),
        };

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(()
            => new JwtSecurityTokenHandler().ValidateToken(jwt, validation, out var securityToken));
    }
}
