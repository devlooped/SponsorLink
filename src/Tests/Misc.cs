using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octokit;
using Spectre.Console;
using static Devlooped.Helpers;

namespace Devlooped.Sponsors;

public class Misc(ITestOutputHelper output)
{
    public record TypedConfig
    {
        public required string Bar { get; init; }
        public required bool Auto { get; init; }
    }

    [Fact]
    public void WriteIni()
    {
        if (File.Exists(".netconfig"))
            File.Delete(".netconfig");

        var config = new ConfigurationBuilder()
            .AddDotNetConfig(".netconfig")
            .Build();

        // Can read and write just using config extensions
        config["foo:bar"] = "baz";
        config["foo:auto"] = "true";

        var saved = new ConfigurationBuilder().AddDotNetConfig(".netconfig").Build();

        Assert.Equal("baz", saved["foo:bar"]);

        var services = new ServiceCollection()
            .Configure<TypedConfig>(saved.GetSection("foo"))
            .BuildServiceProvider();

        var typed = services.GetRequiredService<IOptions<TypedConfig>>().Value;

        Assert.NotNull(typed);
        Assert.Equal("baz", typed.Bar);
        Assert.True(typed.Auto);
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetDeletedIssue()
    {
        var github = new GitHubClient(new Octokit.ProductHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion))
        {
            Credentials = new Credentials(Configuration["GitHub:Token"])
        };

        var ex = await Assert.ThrowsAsync<ApiException>(async () => await github.Issue.Get("devlooped", "sandbox", 20));

    }

    [SecretsFact("Azure:SubscriptionId", "Azure:ResourceGroup", "Azure:LogAnalytics")]
    public async Task GetPurgeStatus()
    {
        // Purge endpoint is https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.OperationalInsights/workspaces/{workspace}/purge?api-version=2020-08-01
        // See https://learn.microsoft.com/en-us/rest/api/loganalytics/workspace-purge/purge?view=rest-loganalytics-2023-09-01&tabs=HTTP
        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]));

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        // Sample ID
        var purgeId = "purge-6a89f2a1-baeb-4722-845f-51f33dcc4c2c";

        // Get status, see https://learn.microsoft.com/en-us/rest/api/loganalytics/workspace-purge/get-purge-status?view=rest-loganalytics-2023-09-01&tabs=HTTP
        var url = $"https://management.azure.com/subscriptions/{Configuration["Azure:SubscriptionId"]}/resourceGroups/{Configuration["Azure:ResourceGroup"]}/providers/Microsoft.OperationalInsights/workspaces/{Configuration["Azure:LogAnalytics"]}/operations/{purgeId}?api-version=2020-08-01";

        var response = await httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadFromJsonAsync<PurgeStatus>();
            output.WriteLine(responseBody?.status);
        }
        else
        {
            output.WriteLine($"Request failed with status code {response.StatusCode}");
        }
    }

    record PurgeStatus(string status);

    [SecretsFact("Pushover:Token", "Pushover:Key")]
    public async Task PushMessage()
    {
        var options = new PushoverOptions();
        Configuration.Bind("Pushover", options);
        var pushover = new Pushover(Services.GetRequiredService<IHttpClientFactory>(), Options.Create(options));

        await pushover.PostAsync(new PushoverMessage
        {
            Title = $"🐛 by kzu as Silver sponsor",
            Message = "Add optional endpoint that can emit shields endpoint badge data",
            Url = "https://github.com/devlooped/SponsorLink/pull/258",
            UrlTitle = $"View Issue #{258}",
            Priority = PushoverPriority.High
        });
    }

    public static void SponsorsAscii()
    {
        var heart =
            """        
              xxxxxxxxxxx      xxxxxxxxxx
             xxxxxxxxxxxxxx   xxxxxxxxxxxxx
            xxxxxxxxxxxxxxxx xxxxxxxxxxxxxx
            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
             xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
              xxxxxxxxxxxxxxxxxxxxxxxxxxx
                xxxxxxxxxxxxxxxxxxxxxx
                   xxxxxxxxxxxxxxxxx
                    xxxxxxxxxxxx
                      xxxxxxxxx
                        xxxxx
                         xxx
                         xx
                         x
            """.Split(Environment.NewLine);

        var canvas = new Canvas(32, 32);
        for (var y = 0; y < heart.Length; y++)
        {
            for (var x = 0; x < heart[y].Length; x++)
            {
                if (heart[y][x] == 'x')
                {
                    canvas.SetPixel(x, y, Color.Purple);
                }
            }
        }

        AnsiConsole.Write(canvas);
    }
}
