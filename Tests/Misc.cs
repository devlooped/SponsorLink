using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        
        var octo = new GitHubClient(new Octokit.ProductHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2)))
        {
            Credentials = new Credentials(accessToken)
        };

        //var installations = await octo.GitHubApps.GetAllInstallationsForCurrentUser();
        //Output.WriteLine(JsonConvert.SerializeObject(installations, Formatting.Indented));

        //resp = await http.GetAsync("https://api.github.com/app/installations", jwt);
        //var body = await resp.Content.ReadAsStringAsync();

        //resp = await http.PostAsync($"https://api.github.com/app/installations/{installation}/access_tokens", null, jwt);
        //body = await resp.Content.ReadAsStringAsync();
        //data = JsonConvert.DeserializeObject(body) ?? throw new InvalidOperationException();

        //var query =
        //    """
        //    query ($owner: String!, $endCursor: String) {
        //      user(login: $owner) {
        //        sponsorshipsAsSponsor(first: 100, after: $endCursor) {
        //          nodes {
        //            privacyLevel
        //            tier {
        //              monthlyPriceInDollars
        //            }
        //            sponsorable {
        //              ... on User {
        //                	id
        //                	login
        //              }
        //              ... on Organization {
        //                	id
        //                	login
        //              }
        //            }
        //          }
        //        }
        //      }
        //    }
        //    """;

        //http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)data.token);
        //var response = await http.PostAsJsonAsync("https://api.github.com/graphql", 
        //    new 
        //    { 
        //        query,
        //        variables = new
        //        {
        //            owner = user.Login
        //        }
        //    });
        //body = await response.Content.ReadAsStringAsync();

        var query =
            """
            query {
              viewer{
                sponsoring(first: 100) {
                  nodes {
                    ... on User {
                      login
                    }
                    ... on Organization {
                      login
                    }
                  }
                }
              }
            }
            """;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2)));

        var owner = await octo.User.Current();

        var response = await http.PostAsJsonAsync("https://api.github.com/graphql",
            new
            {
                query,
                variables = new
                {
                    owner = owner.Login
                }
            });

        var body = await response.Content.ReadAsStringAsync();
    }
}