using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Devlooped.Sponsors;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder Configure(this IConfigurationBuilder builder)
    {
        builder.AddUserSecrets("A85AC898-E41C-4D9D-AD9B-52ED748D9901");
        // Optionally, use key vault for secrets instead of plain-text app service configuration
        if (Environment.GetEnvironmentVariable("AZURE_KEYVAULT") is string kv)
            builder.AddAzureKeyVault(new Uri($"https://{kv}.vault.azure.net/"), new DefaultAzureCredential());

#if DEBUG
        // Allows using SL config for local development. 
        // In particular, the telemetry module will inject the local id as if had been received from 'x-telemetry-id' header 
        // when testing locally via the browser for easier API testing.
        builder.AddDotNetConfig(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sponsorlink"));
#endif

        return builder;
    }
}
