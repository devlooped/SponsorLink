using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
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
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
        services
            .AddOptions<SponsorLinkOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("SponsorLink").Bind(options);
            });

        this.services = services.BuildServiceProvider();
        httpFactory = this.services.GetRequiredService<IHttpClientFactory>();

        principal = new AsyncLazy<ClaimsPrincipal>(async () =>
        {
            var login = await httpFactory.CreateClient("sponsor").QueryAsync<Account>(GraphQueries.ViewerAccount);
            Assert.NotNull(login);
            return new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("urn:github:login", login.Login) }, "github"));
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
            services.GetRequiredService<IMemoryCache>(),
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorType.None, await manager.GetSponsorAsync());
    }

    [SecretsFact("GitHub:Token", "GitHub:Sponsorable")]
    public async Task GetUserOrOrganization()
    {
        using var http = httpFactory.CreateClient("sponsorable");

        Assert.Equal(AccountType.Organization,
            (await http.QueryAsync<Account>(GraphQueries.Sponsorable("devlooped")))?.Type);

        Assert.Equal(AccountType.User,
            (await http.QueryAsync<Account>(GraphQueries.Sponsorable("kzu")))?.Type);
    }

    [SecretsFact("GitHub:PrivateUser", "GitHub:Sponsorable")]
    public async Task GetPrivateUserSponsor()
    {
        configuration["SponsorLink:Account"] = "devlooped";
        configuration["GitHub:Token"] = configuration["GitHub:PrivateUser"];

        await Authenticate();

        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(),
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorType.User, await manager.GetSponsorAsync());
    }

    [SecretsFact("GitHub:Token", "GitHub:PublicOrg")]
    public async Task GetPublicOrgSponsor()
    {
        configuration["SponsorLink:Account"] = "devlooped";
        configuration["GitHub:Token"] = configuration["GitHub:PublicOrg"];

        await Authenticate();

        var manager = new SponsorsManager(
            services.GetRequiredService<IOptions<SponsorLinkOptions>>(),
            httpFactory,
            services.GetRequiredService<IGraphQueryClientFactory>(),
            services.GetRequiredService<IMemoryCache>(),
            Mock.Of<ILogger<SponsorsManager>>());

        Assert.Equal(SponsorType.Organization, await manager.GetSponsorAsync());
    }
}
