using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Azure.Identity;
using Devlooped.Sponsors;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

class Program
{
    static void Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.AddUserSecrets("A85AC898-E41C-4D9D-AD9B-52ED748D9901");
                // Optionally, use key vault for secrets instead of plain-text app service configuration
                if (Environment.GetEnvironmentVariable("AZURE_KEYVAULT") is string kv)
                    builder.AddAzureKeyVault(new Uri($"https://{kv}.vault.azure.net/"), new DefaultAzureCredential());
            })
            .ConfigureFunctionsWebApplication(builder =>
            {
                builder.UseFunctionContextAccessor();
                builder.UseErrorLogging();
        #if DEBUG
                builder.UseGitHubDeviceFlowAuthentication();
        #endif
                builder.UseAppServiceAuthentication();
                builder.UseGitHubAuthentication();
                builder.UseClaimsPrincipal();
            })
            .ConfigureServices(services =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();
                services.AddMemoryCache();

                services.AddOptions();
                JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

                services
                    .AddOptions<SponsorLinkOptions>()
                    .Configure<IConfiguration>((options, configuration) =>
                    {
                        configuration.GetSection("SponsorLink").Bind(options);
                    });

                // Add sponsorable client using the GH_TOKEN for GitHub API access
                services.AddHttpClient("sponsorable", (sp, http) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    if (config["GitHub:Token"] is not { Length: > 0 } ghtoken)
                        throw new InvalidOperationException("Missing required configuration 'GitHub:Token'");

                    http.BaseAddress = new Uri("https://api.github.com");
                    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghtoken);
                });

                // Add sponsor client using the current invocation claims for GitHub API access
                services.AddScoped<AccessTokenMessageHandler>();
                services.AddHttpClient("sponsor", http =>
                {
                    http.BaseAddress = new Uri("https://api.github.com");
                    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ThisAssembly.Info.Product, ThisAssembly.Info.InformationalVersion));
                }).AddHttpMessageHandler<AccessTokenMessageHandler>();

                services.AddGraphQueryClient();

                // RSA key for JWT signing
                services.AddSingleton(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<SponsorLinkOptions>>();
                    if (string.IsNullOrEmpty(options.Value.PrivateKey))
                        throw new InvalidOperationException("Missing required configuration 'SponsorLink:PrivateKey'");

                    // The key (as well as the yaml manifest) can be generated using gh sponsors init
                    // Install with: gh extension install devlooped/gh-sponsors
                    // See: https://github.com/devlooped/gh-sponsors
                    var rsa = RSA.Create();
                    rsa.ImportRSAPrivateKey(Convert.FromBase64String(options.Value.PrivateKey), out _);

                    return rsa;
                });

                services.AddSingleton<SponsorsManager>();
            })
            .Build();

        host.Run();
    }
}