using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

partial class SponsorLink
{
    /// <summary>
    /// Backwards compatibility for pre-beta endpoint.
    /// </summary>
    [Function("legacy-sync")]
    public static IActionResult LegacySyncAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sync")] HttpRequest req)
        => new RedirectResult("me", true, true);

    /// <summary>
    /// Backwards compatibility for pre-beta endpoint.
    /// </summary>
    [Function("legacy-user")]
    public static IActionResult LegacyUserAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user")] HttpRequest req)
        => new RedirectResult("view", true, true);
}