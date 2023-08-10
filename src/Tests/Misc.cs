using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Identity;
using Azure.Monitor.Query;
using Devlooped;
using Devlooped.SponsorLink;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;

namespace Tests;

//public record SponsorData(string Action, )

public record Misc(ITestOutputHelper Output)
{
    [Fact]
    public async Task UnsponsorRegistry()
    {
        var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        if (!CloudStorageAccount.TryParse(config["RealStorageAccountForTests"], out var account))
            return;

        var container = account.CreateBlobServiceClient().GetBlobContainerClient("sponsorlink");

        try
        {
            while (true)
            {
                try
                {
                    await container.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);
                    break;
                }
                catch (RequestFailedException ex) when (ex.Status == 409 && ex.ErrorCode == "ContainerBeingDeleted")
                {
                    // Allow some time for the blob container deletion to complete, across test runs.
                    Thread.Sleep(100);
                }
            }

            var sponsorable = new AccountId("MDEyOk9yZ2FuaXphdGlvbjYxNTMzODE4", "devlooped");
            var sponsor = new AccountId("MDQ6VXNlcjg3OTU5NTQx", "devlooped-bot");
            var registry = new SponsorsRegistry(account, Mock.Of<IEventStream>());

            await registry.RegisterSponsorAsync(sponsorable, sponsor, new[] { "kzu@github.com", "test@github.com" });

            await registry.UnregisterSponsorAsync(sponsorable, sponsor);
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

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
    public void ParseIni()
    {
        var text = ThisAssembly.Resources.settings.Text;
        var values = text.Split(new[] { "\r\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x[0] != '#')
            .Select(x => x.Split(new[] { '=' }, 2))
            .ToDictionary(x => x[0].Trim(), x => x[1].Trim());

        foreach (var pair in values)
        {
            Output.WriteLine($"{pair.Key} = {pair.Value}");
        }
    }

    [Fact]
    public async Task GetInstallations()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(ThisAssembly.Project.UserSecretsId)
            .Build();

        var accessToken = config["AccessToken"];

        if (string.IsNullOrEmpty(accessToken))
            return;

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

    [Fact]
    public async Task GetOrganizations()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(ThisAssembly.Project.UserSecretsId)
            .Build();

        var accessToken = config["AccessToken"];
        if (string.IsNullOrEmpty(accessToken))
            return;

        var octo = new GitHubClient(new Octokit.ProductHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2)))
        {
            Credentials = new Credentials(accessToken)
        };
        var user = await octo.User.Current();
        Console.WriteLine($"{user.Login}");

        var query =
            """
            query {
              viewer{
                organizations(first: 100) {
                  nodes {
                    id
                    login
                  }
                }
              }
            }
            """;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2)));

        var response = await http.PostAsJsonAsync("https://api.github.com/graphql", new { query });

        Assert.True(response.IsSuccessStatusCode);

        var body = await response.Content.ReadAsStringAsync();
        foreach (var account in JObject.Parse(body)
            .SelectTokens("$.data.viewer.organizations.nodes[*]")
            .Select(j => j.ToString())
            .Select(JsonConvert.DeserializeObject<AccountId>))
        {
            Output.WriteLine($"{account?.Login} ({account?.Id})");
        }
    }

    [Fact]
    public async Task QueryLogs()
    {
        var creds = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
        });

        var client = new LogsQueryClient(creds);
        var kql =
            """
            StorageBlobLogs
            | where TimeGenerated > ago(6d)
            | where OperationName == "GetBlobProperties"
            | extend Parts=split(ObjectKey, '/')
            | extend Project=tostring(Parts[2]), Container=tostring(Parts[3]), Email=tostring(Parts[4])
            | where Project == "sponsorlink" and Container == "apps"
            | extend Url=parse_url(Uri)
            | extend Account=tostring(Url["Query Parameters"]["account"]), 
                     Product=tostring(Url["Query Parameters"]["product"]), 
                     Package=tostring(Url["Query Parameters"]["package"]), 
                     Version=tostring(Url["Query Parameters"]["version"])
            | where not(isempty(Account)) and not(isempty(Product)) and Account != "kzu" and Account != "sample"
            | project TimeGenerated, Email, 
                      Account=iif(isempty(Account), "devlooped", Account),
                      Product=iif(isempty(Product), "unknown", Product),
                      Package=iif(isempty(Package), iif(isempty(Product), "unknown", Product), Package),
                      Version
            | summarize Count=count() by Account, Product, Package, Version, Email
            """;

        var result = await client.QueryWorkspaceAsync<LogResult>("b5823173-1be6-4d23-92e6-4d2a4a89ad20", kql,
            new QueryTimeRange(DateTimeOffset.UtcNow.AddDays(-6), DateTimeOffset.UtcNow));

    }

    public class LogResult
    //(string? Account, string? Product, string? Package, string? Version, string? Email, long? Count)
    {
        public string? Account { get; set; }
        public string? Product { get; set; }
        public string? Package { get; set; }
        public string? Version { get; set; }
        public string? Email { get; set; }
        public long? Count { get; set; }

    }
}