using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Devlooped.SponsorLink;

public class Functions
{
    readonly SponsorsManager manager;
    readonly IConfiguration configuration;
    readonly IHttpClientFactory clientFactory;
    readonly IEventStream events;
    readonly ITablePartition<Webhook> webhooks;

    public Functions(SponsorsManager manager, IConfiguration configuration, IHttpClientFactory clientFactory, IEventStream events, CloudStorageAccount account)
        => (this.manager, this.configuration, this.clientFactory, this.events, webhooks)
        = (manager, configuration, clientFactory, events, TablePartition.Create<Webhook>(account, "Webhook", "SponsorLink", x => x.Id));

    record EmailInfo(string email, bool verified);

    public record PingCompleted(DateTimeOffset When);

    [FunctionName("ping")]
    public async Task<IActionResult> Ping([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")] HttpRequest req)
    {
        await events.PushAsync(new PingCompleted(DateTimeOffset.Now));
        return new OkObjectResult("pong");
    }

    [FunctionName("expirations")]
    public async Task CheckExpirationsAsync([TimerTrigger("0 0 0 * * *")] TimerInfo timer)
        => await manager.UnsponsorExpiredAsync(DateOnly.FromDateTime(DateTime.Today));

    [FunctionName("authorize")]
    public async Task<IActionResult> AuthorizeAppAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "authorize/{kind}")] HttpRequest req, string kind)
    {
        var appKind = Enum.Parse<AppKind>(kind, true);
        var code = req.Query["code"].ToString();
        
        // TODO: the installation id can be used to request an installation token to perform actions 
        // authorized to the app on behalf of the authorizing user.
        // This will be necessary for the sponsorable account if we ever figure out how to run GraphQL 
        // queries, but for now, it's impossible.
        //var installation = req.Query["installation_id"].ToString();
        
        await manager.AuthorizeAsync(appKind, code);

        return new RedirectResult("https://devlooped.com");
    }

    [FunctionName("app")]
    public async Task<IActionResult> AppHookAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/{kind}")] HttpRequestMessage req, string kind)
    {
        var body = await req.Content!.ReadAsStringAsync();

        if (!SecurityManager.VerifySignature(body, configuration["GitHub:WebhookSecret"], req.Headers.GetValues("x-hub-signature-256").FirstOrDefault()))
            return new BadRequestResult();

        dynamic? payload = JsonConvert.DeserializeObject(body);
        if (payload == null)
            return new BadRequestObjectResult("Could not deserialize payload as JSON");

        await webhooks.PutAsync(new(
            req.Headers.GetValues("X-GitHub-Delivery").FirstOrDefault() ?? Guid.NewGuid().ToString(),
            ((JToken)payload).ToString(Formatting.Indented)));

        string action = payload.action;
        var appKind = Enum.Parse<AppKind>(kind, true);
        var id = new AccountId((string)payload.installation.account.node_id, (string)payload.installation.account.login);
        var note = $"App {appKind} {action} on {payload.installation.account.login} by {payload.sender.login}";

        await (action switch
        {
            "created" => manager.AppInstallAsync(appKind, id, note),
            "deleted" => manager.AppUninstallAsync(appKind, id, note),
            "suspend" => manager.AppSuspendAsync(appKind, id, note),
            "unsuspend" => manager.AppUnsuspendAsync(appKind, id, note),
            _ => Task.CompletedTask,
        });

        return new OkObjectResult(note);
    }

    [FunctionName("sponsor")]
    public async Task<IActionResult> SponsorHookAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sponsor/{account}")] HttpRequestMessage req, string account)
    {
        var body = await req.Content!.ReadAsStringAsync();        
        dynamic? payload = JsonConvert.DeserializeObject(body);
        if (payload == null)
            return new BadRequestObjectResult("Could not deserialize payload as JSON");

        string action = payload.action;
        if (action == null)
            return new OkObjectResult("Nothing to do :)");

        var sponsorable = new AccountId((string)payload.sponsorship.sponsorable.node_id, (string)payload.sponsorship.sponsorable.login);
        var sponsor = new AccountId((string)payload.sponsorship.sponsor.node_id, (string)payload.sponsorship.sponsor.login);
        int amount = payload.sponsorship.tier.monthly_price_in_dollars;
        bool oneTime = payload.sponsorship.tier.is_one_time;
        DateTime date = payload.sponsorship.created_at;

        var installation = await manager.FindAppAsync(AppKind.Sponsorable, sponsorable);
        if (installation == null)
            return new BadRequestObjectResult($"No SponsorLink Admin installation found for {sponsorable}. See https://github.com/apps/sponsorlink-admin");

        // We require the installation to be present and enabled to receive sponsorships
        if (installation == null ||
            !SecurityManager.VerifySignature(body, installation.Secret, req.Headers.GetValues("x-hub-signature-256").FirstOrDefault()))
            return new BadRequestObjectResult($"Could not verify signature payload signature. See https://github.com/apps/sponsorlink-admin");

        await webhooks.PutAsync(new(
            req.Headers.GetValues("X-GitHub-Delivery").FirstOrDefault() ?? Guid.NewGuid().ToString(),
            ((JToken)payload).ToString(Formatting.Indented)));

        if (action == "created")
        {
            var note = $"{sponsor.Login} started sponsoring {sponsorable.Login} with ${amount}";
            if (oneTime)
                note += " (one-time)";

            await manager.SponsorAsync(sponsorable, sponsor, amount, oneTime ? DateOnly.FromDateTime(date).AddDays(30) : null, note);
        }
        else if (action == "cancelled")
        {
            await manager.UnsponsorAsync(sponsorable, sponsor,
                $"{sponsor.Login} cancelled sponsorship of {sponsorable.Login}.");
        }
        else if (action == "tier_changed")
        {
            int from = payload.changes.tier.from.monthly_price_in_dollars;
            var note = $"{sponsor.Login} updated sponsorship of {sponsorable.Login} from ${from} to ${amount}";

            await manager.SponsorUpdateAsync(sponsorable, sponsor, amount, note);
        }
        
        // TODO: if sponsorable has not installed the admin app or is 
        // not sponsoring devlooped with at least $x, return OK with content explaining 
        // this and that checks won't work until they do.

        return new OkResult();
    }
}
