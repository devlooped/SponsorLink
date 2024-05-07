using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

static class GitHubAuthExtensions
{
    public static bool TryGetClientId(this IConfiguration configuration, ILogger logger, [NotNullWhen(true)] out string? clientId)
    {
        clientId = configuration["WEBSITE_AUTH_GITHUB_CLIENT_ID"];

#if DEBUG
        // Make it easier for local development by allowing the client ID to be set in user secrets.
        // This simplifies the process of running the app locally without having to set up App Service authentication, 
        // which is all but impossible to do locally.
        clientId ??= configuration["GitHub:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("Missing required configuration/secret 'GitHub:ClientID'.");
            return false;
        }
        return true;
#endif

        if (!bool.TryParse(configuration["WEBSITE_AUTH_ENABLED"], out var authEnabled) || !authEnabled)
        {
            logger.LogError("Ensure App Service authentication is enabled.");
            return false;
        }

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogError("Ensure GitHub identity provider is configured in App Service authentication.");
            return false;
        }

        return true;
    }
}
