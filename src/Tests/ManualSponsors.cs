using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using Octokit;

namespace Devlooped.SponsorLink.GraphQL;

public record SponsorsResult(SponsorsData Data);
public record SponsorsData(Sponsorable? Organization, Sponsorable? User);
public record Sponsorable(string Id, string Login, Sponsorships SponsorshipsAsMaintainer);
public record Sponsorships(Sponsorship[] Nodes);
public record Sponsorship(DateTime CreatedAt, bool IsOneTimePayment, Sponsor SponsorEntity, Tier Tier);
public record Sponsor(string Id, string Login);
public record Tier(int MonthlyPriceInDollars);

public record ManualSponsors(ITestOutputHelper Output)
{
    [Fact]
    public async Task RegisterExistingSponsors()
    {
        var json =
            """
            {
              "data": {
                "organization": {
                  "id": "MDEyOk9yZ2FuaXphdGlvbjYxNTMzODE4",
                  "login": "devlooped",
                  "sponsorshipsAsMaintainer": {
                    "nodes": [
                      {
                        "createdAt": "2020-12-22T05:29:42Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDEyOk9yZ2FuaXphdGlvbjcxODg4NjM2",
                          "login": "clarius"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 1
                        }
                      },
                      {
                        "createdAt": "2021-01-24T10:15:18Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjE2Njk3NTQ3",
                          "login": "MelbourneDeveloper"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 2
                        }
                      },
                      {
                        "createdAt": "2021-02-17T04:35:26Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjE3NzYwOA==",
                          "login": "augustoproiete"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 1
                        }
                      },
                      {
                        "createdAt": "2022-02-03T07:24:51Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjY3OTMyNg==",
                          "login": "KirillOsenkov"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 10
                        }
                      },
                      {
                        "createdAt": "2022-04-02T16:09:20Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDEyOk9yZ2FuaXphdGlvbjg3MTgxNjMw",
                          "login": "MFB-Technologies-Inc"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 10
                        }
                      },
                      {
                        "createdAt": "2022-07-17T12:30:08Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjMyMTg2OA==",
                          "login": "sandrock"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 2
                        }
                      },
                      {
                        "createdAt": "2022-09-17T01:58:25Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjUxNTc3NA==",
                          "login": "agocke"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 2
                        }
                      },
                      {
                        "createdAt": "2023-01-20T14:26:28Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjg3OTU5NTQx",
                          "login": "devlooped-bot"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 2
                        }
                      },
                      {
                        "createdAt": "2023-01-11T14:21:37Z",
                        "isOneTimePayment": true,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjU0MTI2ODM=",
                          "login": "aguskzu"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 1
                        }
                      },
                      {
                        "createdAt": "2023-01-10T15:26:29Z",
                        "isOneTimePayment": false,
                        "sponsorEntity": {
                          "id": "MDQ6VXNlcjUyNDMxMDY0",
                          "login": "anycarvallo"
                        },
                        "tier": {
                          "monthlyPriceInDollars": 3
                        }
                      }
                    ]
                  }
                }
              }
            }
            """;

        var result = JsonSerializer.Deserialize<SponsorsResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);

        var config = new ConfigurationBuilder().AddUserSecrets(ThisAssembly.Project.UserSecretsId).Build();
        if (!CloudStorageAccount.TryParse(config["ProductionStorageAccount"], out var account))
            Assert.Fail("Did not find a user secret named 'ProductionStorageAccount' to run the query against.");

        var events = new EventStream(config, new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration());

        var manager = new SponsorsManager(
            Mock.Of<IHttpClientFactory>(),
            new SecurityManager(config),
            account,
            new TableConnection(account, "SponsorLink"),
            //new EventStream(config, new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration()),
            events,
            new SponsorsRegistry(account, events));

        if (result.Data.Organization != null)
        {
            var sponsorable = new AccountId(result.Data.Organization.Id, result.Data.Organization.Login);
            foreach (var sponsorship in result.Data.Organization.SponsorshipsAsMaintainer.Nodes)
            {
                var sponsor = new AccountId(sponsorship.SponsorEntity.Id, sponsorship.SponsorEntity.Login);
                var note = $"Existing sponsor {sponsor.Login} for {sponsorable.Login} with ${sponsorship.Tier.MonthlyPriceInDollars} {(sponsorship.IsOneTimePayment ? "one-time" : "monthly")} payment";

                await manager.SponsorAsync(sponsorable, sponsor,
                    sponsorship.Tier.MonthlyPriceInDollars,
                    sponsorship.IsOneTimePayment ? DateOnly.FromDateTime(sponsorship.CreatedAt.AddDays(30)) : null,
                    note);
            }
        }
    }
}
