using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Devlooped.Sponsors.Tests;

public class SponsorManagerTests : IDisposable
{
    IServiceProvider services;
    IHttpClientFactory httpFactory;
    IConfiguration configuration;
    AsyncLazy<ClaimsPrincipal> principal;

    public SponsorManagerTests()
    {
        configuration = new ConfigurationBuilder()
            .AddUserSecrets<SponsorManagerTests>()
            .Build();

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddMemoryCache();

        // Simulates the registration of the services in the Web app
        services.AddHttpClient("sponsor", (sp, http) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["GH_SPONSOR"] is not { Length: > 0 } ghtoken)
                throw new InvalidOperationException("Missing required configuration GH_TOKEN");

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink.Tests", ThisAssembly.Info.InformationalVersion));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
        });

        services.AddHttpClient("sponsorable", (sp, http) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["GH_TOKEN"] is not { Length: > 0 } ghtoken)
                throw new InvalidOperationException("Missing required configuration GH_TOKEN");

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink.Tests", ThisAssembly.Info.InformationalVersion));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
        });

        services.AddSingleton<IConfiguration>(configuration);
        this.services = services.BuildServiceProvider();
        httpFactory = this.services.GetRequiredService<IHttpClientFactory>();

        principal = new AsyncLazy<ClaimsPrincipal>(async () =>
        {
            var login = await httpFactory.CreateClient("sponsor").QueryAsync(GraphQueries.ViewerLogin);
            return new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("urn:github:login", login) }, "github"));
        });
    }

    void IDisposable.Dispose() => ClaimsPrincipal.ClaimsPrincipalSelector = null!;

    async Task Authenticate()
    {
        var principal = await this.principal;
        ClaimsPrincipal.ClaimsPrincipalSelector = () => principal;
    }

    [SecretsFact("GH_SPONSOR", "GH_TOKEN")]
    public async Task AnonymousUserIsNoSponsor()
    {
        var manager = new SponsorsManager(
            configuration, httpFactory,
            services.GetRequiredService<IMemoryCache>(),
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorType.None, await manager.GetSponsorAsync());
    }

    [SecretsFact("GH_SPONSOR", "GH_TOKEN")]
    public async Task GetManifestOverridesTokenAccountFromConfig()
    {
        var manager = new SponsorsManager(
            configuration, httpFactory,
            services.GetRequiredService<IMemoryCache>(),
            Mock.Of<ILogger<SponsorsManager>>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.GetManifestAsync());

        configuration["SPONSORLINK_ACCOUNT"] = "devlooped";

        var manifest = await manager.GetManifestAsync();

        Assert.Equal("devlooped", manifest.Audience);
    }

    [SecretsFact("GH_SPONSOR", "GH_TOKEN")]
    public async Task GetUserOrOrganization()
    {
        using var http = httpFactory.CreateClient("sponsorable");

        Assert.Equal(AccountType.Organization,
            (await http.QueryAsync<Sponsorable>(GraphQueries.Sponsorable("devlooped")))?.Type);

        Assert.Equal(AccountType.User,
            (await http.QueryAsync<Sponsorable>(GraphQueries.Sponsorable("kzu")))?.Type);
    }

    [SecretsFact("GH_TOKEN", "GH_USER_PRIVATE")]
    public async Task GetPrivateUserSponsor()
    {
        configuration["SPONSORLINK_ACCOUNT"] = "devlooped";
        configuration["GH_SPONSOR"] = configuration["GH_USER_PRIVATE"];

        await Authenticate();

        var manager = new SponsorsManager(
            configuration, httpFactory,
            services.GetRequiredService<IMemoryCache>(),
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorType.User, await manager.GetSponsorAsync());
    }

    [SecretsFact("GH_TOKEN", "GH_ORG_PUBLIC")]
    public async Task GetPublicOrgSponsor()
    {
        configuration["SPONSORLINK_ACCOUNT"] = "devlooped";
        configuration["GH_SPONSOR"] = configuration["GH_ORG_PUBLIC"];

        await Authenticate();

        var manager = new SponsorsManager(
            configuration, httpFactory,
            services.GetRequiredService<IMemoryCache>(),
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorType.Organization, await manager.GetSponsorAsync());
    }
}
