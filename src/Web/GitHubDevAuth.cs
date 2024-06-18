using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Devlooped.Sponsors;

#if DEBUG
class GitHubDevAuth
{
    /// <summary>
    /// This helper callback function makes sure we can have the same callback URL format in the dev GH OAuth app 
    /// and the real production app (save for the host name, via ngrok). 
    /// It's never compiled and deployed to Azure functions, though, which already provides this via EasyAuth.
    /// </summary>
    [Function("github")]
    public static IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".auth/login/github/callback")] HttpRequest req, string state)
    {
        if (state?.StartsWith("redir=") == true)
            return new RedirectResult(state[6..]);

        return new RedirectResult("/me");
    }
}
#endif