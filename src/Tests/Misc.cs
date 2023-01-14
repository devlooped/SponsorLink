using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Octokit;

namespace Tests;

//public record SponsorData(string Action, )

public record Misc(ITestOutputHelper Output)
{
    [Fact]
    public void EncodeTicks()
    {
        Output.WriteLine(Base62.Encode(DateTimeOffset.Now.UtcTicks - QuaranTime.Epoch.UtcTicks));
        Output.WriteLine(Base62.Encode(DateTimeOffset.Now.UtcTicks - QuaranTime.Epoch.UtcTicks));
        Output.WriteLine(Base62.Encode(DateTimeOffset.Now.UtcTicks - QuaranTime.Epoch.UtcTicks));
    }

    [Fact]
    public void Encode()
    {
        var code = Base62.Encode(DateTimeOffset.UtcNow.ToQuaranTimeMilliseconds());
        Output.WriteLine(Base62.Encode(Thread.CurrentThread.ManagedThreadId) + "." + code);

        Output.WriteLine(Base62.Encode(BigInteger.Abs(new BigInteger(Guid.NewGuid().ToByteArray()))));

        var payload = ThisAssembly.Resources.webhook_created.Text;
        var mem = new MemoryStream();
        using (var compressor = new DeflateStream(mem, CompressionMode.Compress, true))
        using (var writer = new StreamWriter(compressor))
            writer.Write(payload);
        
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(payload));
        var big = BigInteger.Abs(new BigInteger(hash));
        Output.WriteLine($"{Encoding.UTF8.GetByteCount(payload)} bytes, {hash.Length} hashed: {Base62.Encode(big)}");

        hash = SHA1.HashData(mem.ToArray());
        big = BigInteger.Abs(new BigInteger(hash));
        Output.WriteLine($"{mem.Length} bytes, {hash.Length} hashed: {Base62.Encode(big)}");

        Output.WriteLine("Email hash: " + Base62.Encode(BigInteger.Abs(new BigInteger(SHA256.HashData(Encoding.UTF8.GetBytes("daniel@cazzulino.com"))))));
    }

    [Fact]
    public void Deserialize()
    {
        dynamic data = JsonConvert.DeserializeObject(ThisAssembly.Resources.sponsors_created.Text)!;

        DateTime date = data.sponsorship.created_at;

        Output.WriteLine(date.AddDays(30).ToString("o"));
    }

    [Fact]
    public async Task GetInstallations()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(ThisAssembly.Project.UserSecretsId)
            .Build();

        var accessToken = config["AccessToken"];
        
        var octo = new GitHubClient(new ProductHeaderValue("Devlooped-Sponsor", new Version(ThisAssembly.Info.Version).ToString(2)))
        {
            Credentials = new Credentials(accessToken)
        };

        var installations = await octo.GitHubApps.GetAllInstallationsForCurrentUser();
        
    }
}