using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace Devlooped.Sponsors.Tests;

public sealed class SponsorManagerTests : IDisposable
{
    IServiceProvider services;
    IHttpClientFactory httpFactory;
    IGraphQueryClientFactory clientFactory;
    IConfiguration configuration;
    AsyncLazy<ClaimsPrincipal> principal;
    static AsyncLazy<OpenSource> oss;

    static SponsorManagerTests()
    {
        oss = new AsyncLazy<OpenSource>(async () =>
        {
            using var http = new HttpClient();
            var response = await http.GetAsync("https://raw.githubusercontent.com/devlooped/nuget/refs/heads/main/nuget.json");

            Assert.True(response.IsSuccessStatusCode);

            var data = await JsonSerializer.DeserializeAsync<OpenSource>(response.Content.ReadAsStream(), JsonOptions.Default);
            Assert.NotNull(data);

            return data;
        });
    }

    public SponsorManagerTests()
    {
        configuration = new ConfigurationBuilder()
            .AddUserSecrets<SponsorManagerTests>()
            .Build();

        var services = new ServiceCollection();
        services.AddMemoryCache();

        services.AddHttpClient(Options.DefaultName, c => c.BaseAddress = new Uri("https://api.github.com"));

        // Simulates the registration of the services in the Web app
        services.AddHttpClient("sponsor", (sp, http) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["GitHub:Token"] is not { Length: > 0 } ghtoken)
                throw new InvalidOperationException("Missing required configuration GitHub:Token");

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink.Tests", ThisAssembly.Info.InformationalVersion));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
        });

        services.AddHttpClient("sponsorable", (sp, http) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            if (config["GitHub:Sponsorable"] is not { Length: > 0 } ghtoken)
                throw new InvalidOperationException("Missing required configuration GitHub:Sponsorable");

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink.Tests", ThisAssembly.Info.InformationalVersion));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
        });

        services.AddGraphQueryClient();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions();
        JsonWebTokenHandler.DefaultMapInboundClaims = false;

        services
            .AddOptions<SponsorLinkOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("SponsorLink").Bind(options);
            });

        this.services = services.BuildServiceProvider();
        httpFactory = this.services.GetRequiredService<IHttpClientFactory>();
        clientFactory = this.services.GetRequiredService<IGraphQueryClientFactory>();

        principal = new AsyncLazy<ClaimsPrincipal>(async () =>
        {
            var login = await clientFactory.CreateClient("sponsor").QueryAsync(GraphQueries.ViewerAccount);
            Assert.NotNull(login);

            var claims = new List<Claim>()
            {
                new("urn:github:login", login.Login)
            };

            var emails = await clientFactory.CreateClient("sponsor").QueryAsync(GraphQueries.ViewerEmails);
            if (emails?.Length > 0)
                claims.AddRange(emails.Select(x => new Claim(ClaimTypes.Email, x)));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "github"));
        });
    }

    void IDisposable.Dispose() => ClaimsPrincipal.ClaimsPrincipalSelector = null!;

    async Task Authenticate()
    {
        var principal = await this.principal;
        ClaimsPrincipal.ClaimsPrincipalSelector = () => principal;
    }

    [SecretsFact("GitHub:Token", "GitHub:Sponsorable")]
    public async Task AnonymousUserIsNoSponsor()
    {
        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(), oss,
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorTypes.None, await manager.GetSponsorTypeAsync());
    }

    [SecretsFact("GitHub:Token", "GitHub:Sponsorable")]
    public async Task GetUserOrOrganization()
    {
        var graph = clientFactory.CreateClient("sponsorable");

        Assert.Equal(AccountType.Organization,
            (await graph.QueryAsync(GraphQueries.Sponsorable("devlooped")))?.Type);

        Assert.Equal(AccountType.User,
            (await graph.QueryAsync(GraphQueries.Sponsorable("kzu")))?.Type);
    }

    [SecretsFact("GitHub:PrivateUser", "GitHub:Sponsorable")]
    public async Task PrivateUserIsMemberOfSponsorable()
    {
        configuration["GitHub:Token"] = configuration["GitHub:PrivateUser"];

        await Authenticate();

        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(), oss,
            Mock.Of<ILogger<SponsorsManager>>());

        var types = await manager.GetSponsorTypeAsync();
        Assert.True(types.HasFlag(SponsorTypes.Team));
    }

    [SecretsFact("GitHub:Token", "GitHub:PublicOrg")]
    public async Task GetPublicOrgSponsor()
    {
        configuration["GitHub:Token"] = configuration["GitHub:PublicOrg"];

        await Authenticate();

        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(), oss,
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorTypes.Organization, await manager.GetSponsorTypeAsync());
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetSponsorshipViaOrganizationMembership()
    {
        await Authenticate();

        var cache = services.GetRequiredService<IMemoryCache>();
        var manifest = SponsorableManifest.Create(new Uri("https://sl.amazon.com"), [new Uri("https://github.com/aws")], "ASDF1324");
        cache.Set(SponsorsManager.ManifestCacheKey, manifest);
        cache.Set(SponsorsManager.JwtCacheKey, manifest.ToJwt());

        var sponsorable = services.GetRequiredService<IGraphQueryClientFactory>().CreateClient("sponsorable");
        var sponsor = services.GetRequiredService<IGraphQueryClientFactory>().CreateClient("sponsor");

        var graph = new Mock<IGraphQueryClient>();

        // Replace candidates
        graph.Setup(x => x.QueryAsync(GraphQueries.ViewerSponsorableCandidates, It.IsAny<(string, object)[]>()))
            .Returns(() => sponsor.QueryAsync(GraphQueries.UserSponsorableCandidates("paulbartell")));

        // Replace contributions
        graph.Setup(x => x.QueryAsync(GraphQueries.ViewerContributedRepoOwners, It.IsAny<(string, object)[]>()))
            .Returns(() => sponsor.QueryAsync(GraphQueries.UserContributions("paulbartell")));

        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            Mock.Of<IGraphQueryClientFactory>(x =>
                x.CreateClient("sponsorable") == sponsorable &&
                x.CreateClient("sponsor") == graph.Object),
            services.GetRequiredService<IMemoryCache>(), oss,
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorTypes.Team, await manager.GetSponsorTypeAsync());
    }

    [SecretsFact("GitHub:Token")]
    public async Task GetSponsorshipViaOrganizationEmail()
    {
        await Authenticate();

        var sponsorable = services.GetRequiredService<IGraphQueryClientFactory>().CreateClient("sponsorable");
        var sponsor = services.GetRequiredService<IGraphQueryClientFactory>().CreateClient("sponsor");

        var graph = new Mock<IGraphQueryClient>();

        // Replace candidates
        graph.Setup(x => x.QueryAsync(GraphQueries.ViewerSponsorableCandidates, It.IsAny<(string, object)[]>()))
            .Returns(() => sponsor.QueryAsync(GraphQueries.UserSponsorableCandidates("testclarius")));

        // Replace contributions
        graph.Setup(x => x.QueryAsync(GraphQueries.ViewerContributedRepoOwners, It.IsAny<(string, object)[]>()))
            .Returns(() => sponsor.QueryAsync(GraphQueries.UserContributions("testclarius")));

        graph.Setup(x => x.QueryAsync(GraphQueries.ViewerEmails, It.IsAny<(string, object)[]>()))
            .ReturnsAsync(() => ["test@clarius.org"]);

        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            Mock.Of<IGraphQueryClientFactory>(x =>
                x.CreateClient("sponsorable") == sponsorable &&
                x.CreateClient("sponsor") == graph.Object),
            services.GetRequiredService<IMemoryCache>(),
            oss,
            Mock.Of<ILogger<SponsorsManager>>());

        var types = await manager.GetSponsorTypeAsync();

        Assert.True(types.HasFlag(SponsorTypes.Organization));

        // If authenticated user is an oss author, we should have the flag too.
        var login = await sponsor.QueryAsync(GraphQueries.ViewerAccount);
        if (login != null && await oss is { } data && data.Authors.ContainsKey(login.Login))
            Assert.True(types.HasFlag(SponsorTypes.OpenSource));
    }

    [SecretsFact("GitHub:Token", "GitHub:PublicOrg")]
    public async Task GetSponsorshipClaims()
    {
        configuration["GitHub:Token"] = configuration["GitHub:PublicOrg"];

        await Authenticate();

        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(), oss,
            Mock.Of<ILogger<SponsorsManager>>());

        var claims = await manager.GetSponsorClaimsAsync();

        Assert.NotNull(claims);
        Assert.Contains(claims, claim => claim.Type == "roles" && claim.Value == "org");

        var sponsorable = await manager.GetManifestAsync();

        var manifest = SponsorableManifest.Create(new Uri(sponsorable.Issuer), [new Uri("https://github.com/" + sponsorable.Sponsorable)], claims.First(c => c.Type == "client_id").Value);

        var jwt = manifest.Sign(claims);

        Assert.NotNull(jwt);

        var identity = manifest.Validate(jwt, out var token);
        Assert.NotNull(identity);
        Assert.NotNull(token);
        Assert.Equal(token.Issuer, manifest.Issuer);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Expiration will be the first day of next month.
        var expiry = new DateOnly(today.AddMonths(1).Year, today.AddMonths(1).Month, 1);

        Assert.Equal(expiry, DateOnly.FromDateTime(token.ValidTo));
    }

    [SecretsFact("GitHub:Sponsorable")]
    public async Task GetTiersWithMetadata()
    {
        var manager = new SponsorsManager(
            Options.Create<SponsorLinkOptions>(new SponsorLinkOptions { Account = "devlooped" }),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(), oss,
            Mock.Of<ILogger<SponsorsManager>>());

        var tiers = await manager.GetTiersAsync();

        // Meta is populated from <!-- --> comments in the sponsor listing description, 
        // which is used to hold a yaml block with metadata.
        // The metadata is aggregated across all tiers, with the closest to the tier 
        // taking precedence in case of conflicts.

        Assert.NotNull(tiers);
        Assert.NotEmpty(tiers);

        // No tier should have fewer meta items than any previous one.
        // NOTE: one-time tiers do not cascade to monthly tiers (they don't share parent tiers)

        var meta = new HashSet<string>();
        foreach (var tier in tiers.Values.Where(t => t.OneTime))
        {
            Assert.True(tier.Meta.Count >= meta.Count, $"Tier {tier.Name} does not have at least {meta.Count} metadata items");
            meta.AddRange(tier.Meta.Keys);
        }

        meta.Clear();
        foreach (var tier in tiers.Values.Where(t => !t.OneTime))
        {
            Assert.True(tier.Meta.Count >= meta.Count, $"Tier {tier.Name} does not have at least {meta.Count} metadata items");
            meta.AddRange(tier.Meta.Keys);
        }

        Assert.All(tiers.Values, x => Assert.Contains("label", x.Meta.Keys));
        Assert.All(tiers.Values, x => Assert.Contains("color", x.Meta.Keys));
        Assert.All(tiers.Values, x => Assert.NotEmpty(x.Meta["color"]));
    }

    [SecretsTheory("GitHub:Sponsorable")]
    [InlineData("clarius", "basic", SponsorTypes.Organization)]
    [InlineData("torutek-gh", "silver", SponsorTypes.User)]
    [InlineData("KirillOsenkov", "silver", SponsorTypes.User)]
    [InlineData("victorgarciaaprea", "basic", SponsorTypes.Organization)]
    [InlineData("kzu", "team", SponsorTypes.Team)]
    [InlineData("stakx", "oss", SponsorTypes.OpenSource)] // since he's currently active on castle
    [InlineData("test", "none", SponsorTypes.None)] // https://github.com/test not contributing to nugets
    public async Task GetTierForLogin(string login, string tier, SponsorTypes type)
    {
        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(), oss,
            Mock.Of<ILogger<SponsorsManager>>());

        var sponsor = await manager.FindSponsorAsync(login);

        if (type == SponsorTypes.None)
        {
            Assert.Null(sponsor);
            return;
        }

        Assert.NotNull(sponsor);

        Assert.True(sponsor.Tier.Meta.TryGetValue("tier", out var existing));
        Assert.Equal(tier, existing);
        Assert.Equal(type, sponsor.Kind);
    }
}
