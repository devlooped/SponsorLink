using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

partial class SponsorLink
{
    /// <summary>
    /// Backwards compatibility for pre-beta endpoint.
    /// </summary>
    [Function("legacy-sync")]
    public IActionResult LegacySyncAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sync")] HttpRequest req)
        => new RedirectResult("me", true, true);
}