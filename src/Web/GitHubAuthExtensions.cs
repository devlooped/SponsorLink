using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Devlooped.Sponsors;

static class GitHubAuthExtensions
{
    public static bool TryGetClientId(this IConfiguration configuration, ILogger logger, [NotNullWhen(true)] out string? clientId)
    {
        clientId = null;

#if DEBUG
        // Make it easier for local development by allowing the client ID to be set in user secrets.
        // This simplifies the process of running the app locally without having to set up App Service authentication, 
        // which is all but impossible to do locally.
        clientId = configuration["GitHub:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("Missing required configuration/secret 'GitHub:ClientID'.");
            return false;
        }
#endif

        if (!string.IsNullOrEmpty(clientId))
            return true;

        if (!bool.TryParse(configuration["WEBSITE_AUTH_ENABLED"], out var authEnabled) || !authEnabled)
        {
            logger.LogError("Ensure App Service authentication is enabled.");
            return false;
        }

        if (configuration["WEBSITE_AUTH_V2_CONFIG_JSON"] is not { Length: > 0 } json ||
            JObject.Parse(json) is not { } data ||
            data.SelectToken("$.identityProviders.gitHub") is not { } provider ||
            JsonSerializer.Deserialize<GitHubProvider>(provider.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) is not { } github ||
            !github.Enabled)
        {
            logger.LogError("Ensure GitHub identity provider is configured in App Service authentication.");
            return false;
        }

        clientId = github.Registration.ClientId;

        return !string.IsNullOrEmpty(clientId);
    }

    public record GitHubProvider(bool Enabled, Registration Registration);
    public record Registration(string ClientId);
}
