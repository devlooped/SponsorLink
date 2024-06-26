﻿using System.Net.Http.Headers;
using System.Net.Http.Json;
using static Devlooped.Sponsors.Process;

namespace Devlooped;

[Collection("GitHub")]
public class GitHubTests(ITestOutputHelper output)
{
    [SecretsFact("GitHub:Login", "GitHub:Password", "GitHub:ClientId")]
    public void LoginAsync()
    {
        Assert.True(TryExecute("gh", "auth status", out var output), "Expected no auth status");
    }

    [SecretsFact("GitHub:Token")]
    public async Task RunQuery()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink", "0.1"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Helpers.Configuration["GitHub:Token"] ?? throw new ArgumentException());

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

        output.WriteLine(json);

        //var jq = OperatingSystem.IsLinux() ? "lib/jq-linux-amd64" : @"lib\jq-win64.exe";
        //var data = await Cli.Wrap(jq)
        //    .WithArguments(["-r", "[.data.viewer.repositoriesContributedTo.nodes.[].nameWithOwner]"])
        //    .WithStandardInputPipe(PipeSource.FromString(json))
        //    .ExecuteBufferedAsync();

        var data = await JQ.ExecuteAsync(json, "[.data.viewer.repositoriesContributedTo.nodes.[].nameWithOwner]");

        output.WriteLine(data);
    }
}
