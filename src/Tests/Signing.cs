using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

public class Signing(ITestOutputHelper Output)
{
    static Signing()
    {
        IdentityModelEventSource.ShowPII = true;
        IdentityModelEventSource.LogCompleteSecurityArtifact = true;

        // Ensure we have keys for tests
        RSA rsa = RSA.Create(3072);

        File.WriteAllBytes(@"../../../signing.pub", rsa.ExportRSAPublicKey());
        File.WriteAllText(@"../../../signing.txt", Convert.ToBase64String(rsa.ExportRSAPublicKey()), Encoding.UTF8);
        File.WriteAllBytes(@"../../../signing.key", rsa.ExportRSAPrivateKey());

        File.WriteAllBytes(@"../../../signing.pub2", RSA.Create(2048).ExportRSAPublicKey());

        // write in jwk format
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)));
        File.WriteAllText(@"../../../signing.jwk", JsonSerializer.Serialize(jwk, JsonOptions.JsonWebKey), Encoding.UTF8);

        // ensure we can read back from jwt > JsonWebKey
        var key = JsonWebKey.Create(File.ReadAllText(@"../../../signing.jwk", Encoding.UTF8));
    }

    [LocalFact]
    public void CanReadFromKeyVault()
    {
        var config = new ConfigurationBuilder()
            .AddAzureKeyVault(new Uri("https://devlooped.vault.azure.net/"), new DefaultAzureCredential())
            .Build();

        Assert.NotNull(config["SponsorLink:PublicKey"]);

        foreach (var pair in config.AsEnumerable())
        {
            Output.WriteLine($"{pair.Key} = {pair.Value}");
        }
    }

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
    public async Task JwtSigning()
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

        // Serialize the token and return as a string
        var jwt = new JsonWebTokenHandler
        {
            SetDefaultTimesOnTokenCreation = false,
            MapInboundClaims = false
        }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://sponsorlink.devlooped.com/",
            Audience = "devlooped",
            Claims = claims.GroupBy(x => x.Type).ToDictionary(
                x => x.Key,
                x => x.Count() == 1 ? (object)x.First().Value : x.Select(x => x.Value).ToArray()),
            SigningCredentials = signingCredentials,
        });

        var pub = RSA.Create();
        pub.ImportRSAPublicKey(File.ReadAllBytes(@"../../../signing.pub"), out _);

        var validation = new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidAudience = "devlooped",
            ValidIssuer = "https://sponsorlink.devlooped.com/",
            IssuerSigningKey = new RsaSecurityKey(pub)
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(jwt, validation);

        // Validation succeeds
        Assert.Null(result.Exception);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task JwtSponsorableManifest()
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../signing.key"), out _);

        var securityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        var pubJwk = JsonWebKeyConverter.ConvertFromSecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)));

        var claims = new List<Claim>
        {
            new("iss", "https://sponsorlink.devlooped.com/"),
            new("aud", "https://github.com/devlooped"),
            new("client_id", "a82350fb2bae407b3021"),
            new("sub_jwk", JsonSerializer.Serialize(pubJwk, JsonOptions.JsonWebKey), JsonClaimValueTypes.Json),
        };

        // Serialize the token and return as a string
        var jwt = new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false,
        }.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = signingCredentials,
        });

        Output.WriteLine(jwt);

        // Your code block where you're reading and validating JWT with public key

        // Read the public key from the manifest itself before validating
        var token = new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false,
        }.ReadJsonWebToken(jwt);

        var jwkJson = token.Claims.First(c => c.Type == "sub_jwk").Value;
        var jwk = JsonWebKey.Create(jwkJson);
        var jws = new JsonWebKeySet();
        jws.Keys.Add(jwk);
        var jwtKey = jws.GetSigningKeys().First();

        // NOTE: we cannot recreate the RSAPublicKey from the JWK, so we can never 
        // compare the raw string, but we can still validate with either one.

        var result = await new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false,
        }.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidAudience = token.Audiences.First(),
            ValidIssuer = token.Issuer,
            IssuerSigningKey = jwtKey,
        });

        var principal1 = new ClaimsPrincipal(result.ClaimsIdentity);
        var securityToken1 = result.SecurityToken;

        var pubRsa = RSA.Create();
        pubRsa.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);

        result = await new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false,
        }.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            RequireExpirationTime = false,
            ValidAudience = token.Audiences.First(),
            ValidIssuer = token.Issuer,
            IssuerSigningKey = new RsaSecurityKey(pubRsa),
        });

        var principal2 = new ClaimsPrincipal(result.ClaimsIdentity);
        var securityToken2 = result.SecurityToken;

        Assert.Equal(principal1.Claims, principal2.Claims, (Claim a, Claim b) => a.ToString() == b.ToString());
        Assert.Equal(securityToken1.UnsafeToString(), securityToken2.UnsafeToString());

        // Now you can use the principal to extract the claims and do whatever you need to do
        foreach (var claim in principal1.Claims)
        {
            Output.WriteLine($"{claim.Type}: {claim.Value}");
        }
    }

    [Fact]
    public async Task JwtWrongPublicKey()
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(File.ReadAllBytes(@"../../../signing.key"), out _);

        var securityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new Claim("pub", File.ReadAllText(@"../../../signing.txt", Encoding.UTF8)),
        };

        // Serialize the token and return as a string
        var jwt = new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false
        }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://sponsorlink.devlooped.com/",
            Audience = "devlooped",
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = signingCredentials,
        });

        // Your code block where you're reading and validating JWT with public key

        // Read the public key from the manifest itself before validating
        var token = new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false,
        }.ReadJsonWebToken(jwt);

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

        var result = await new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            SetDefaultTimesOnTokenCreation = false,
        }.ValidateTokenAsync(jwt, validation);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(result.Exception);
    }
}
