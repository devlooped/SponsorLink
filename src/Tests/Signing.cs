using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using static Devlooped.SponsorLink;

namespace Devlooped.Sponsors;

public class Signing(ITestOutputHelper Output)
{
    // NOTE: if you want to locally regenerate the keys, uncomment the following line
    // NOTE: if you want to run locally the SL Functions App, you need to set the public 
    // key as Base64 encoded string in the SPONSORLINK_KEY environment variable
    //[Fact]
    public static void CreateKeyPair()
    {
        // Generate key pair
        RSA rsa = RSA.Create(2048);

        File.WriteAllBytes(@"../../../test.pub", rsa.ExportRSAPublicKey());
        File.WriteAllBytes(@"../../../test.key", rsa.ExportRSAPrivateKey());
    }

    [Fact]
    public void WritePublicKey() => Output.WriteLine(Convert.ToBase64String(PublicKey.ExportRSAPublicKey()));

    [Fact]
    public void SignFile()
    {
        var priv = RSA.Create();
        priv.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../test.key"), out _);

        byte[] data = Encoding.UTF8.GetBytes("Hello, world!");

        // Sign data
        byte[] signature = priv.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var pub = RSA.Create();
        pub.ImportRSAPublicKey(File.ReadAllBytes(@"../../../test.pub"), out _);

        // Verify signature using public key
        Assert.True(pub.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void JwtSigning()
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../test.key"), out _);

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
            issuer: "Devlooped",
            audience: "SponsorLink",
            claims: claims,
            expires: DateTime.Today.AddDays(1),
            signingCredentials: signingCredentials);

        // Serialize the token and return as a string
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        // Your code block where you're reading and validating JWT with public key
        var pub = RSA.Create();
        pub.ImportRSAPublicKey(File.ReadAllBytes(@"../../../test.pub"), out _);

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            ValidAudience = "SponsorLink",
            ValidIssuer = "Devlooped",
            IssuerSigningKey = new RsaSecurityKey(pub)
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, validation, out var securityToken);
    }
}
