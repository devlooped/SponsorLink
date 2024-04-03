using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Configuration;
using Octokit;
using Octokit.Internal;

namespace Devlooped;

public class GitHub(ITestOutputHelper Output)
{
    static readonly IConfiguration config  = new ConfigurationBuilder()
        .AddUserSecrets("5af5cb1f-8a66-4238-b07b-032b4cf1a44d")
        .Build();

    [Fact]
    public async Task RunQuery()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink", "0.1"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["GitHub:Token"] ?? throw new ArgumentException());

        var result = await http.PostAsJsonAsync("https://api.github.com/graphql", new
        {
            query =
                """
                query {
                  viewer {
                    repositoriesContributedTo(first: 100, includeUserRepositories: true, contributionTypes: [COMMIT]) {
                      nodes {
                        nameWithOwner,
                        owner {
                          login
                        }
                      }
                    }
                  }
                }
                """
        });

        var json = await result.Content.ReadAsStringAsync();

        Output.WriteLine(json);

        //var jq = OperatingSystem.IsLinux() ? "lib/jq-linux-amd64" : @"lib\jq-win64.exe";
        //var data = await Cli.Wrap(jq)
        //    .WithArguments(["-r", "[.data.viewer.repositoriesContributedTo.nodes.[].nameWithOwner]"])
        //    .WithStandardInputPipe(PipeSource.FromString(json))
        //    .ExecuteBufferedAsync();

        var data = await JQ.ExecuteAsync(json, "[.data.viewer.repositoriesContributedTo.nodes.[].nameWithOwner]");

        Output.WriteLine(data);
    }
}
