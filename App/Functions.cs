using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using Devlooped;
using Devlooped.SponsorLink;
using JWT.Algorithms;
using JWT.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;

namespace Devlooped.SponsorLink;

public class Functions
{
    readonly SponsorsManager manager;
    readonly IConfiguration configuration;
    readonly IHttpClientFactory clientFactory;
    readonly ITablePartition<User> users;
    readonly ITablePartition<EmailUser> emails;

    readonly EventStream events;
    
    public Functions(SponsorsManager manager, IConfiguration configuration, IHttpClientFactory clientFactory, ITablePartition<User> users, ITablePartition<EmailUser> emails, EventStream events)
        => (this.manager, this.configuration, this.clientFactory, this.users, this.emails, this.events) 
        = (manager, configuration, clientFactory, users, emails, events);

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
        => await manager.CheckExpirationsAsync();

    [FunctionName("authorize")]
    public async Task<IActionResult> AuthorizeAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "authorize/{kind}")] HttpRequest req, string kind)
    {
        using var http = clientFactory.CreateClient();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped-Sponsor", new Version(ThisAssembly.Info.Version).ToString(2)));

        //var jwt = JwtBuilder.Create()
        //          .WithAlgorithm(new RS256Algorithm(key, key))
        //          .Issuer(configuration["AppId"])
        //          .AddClaim("iat", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds())
        //          .AddClaim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds())
        //          .Encode();

        //var code = req.Query["code"].ToString();
        //var installation = req.Query["installation_id"].ToString();
        //var action = req.Query["setup_action"].ToString();

        //var resp = await http.PostAsync("https://github.com/login/oauth/access_token",
        //    new StringContent(JsonSerializer.Serialize(new
        //    {
        //        client_id = configuration["AppClientId"],
        //        client_secret = configuration["AppClientSecret"],
        //        code,
        //        redirect_uri = configuration["AppRedirectUri"] ?? "https://sponsors.devlooped.com/authorize"
        //    }), Encoding.UTF8, "application/json"),
        //    jwt);

        //var data = await resp.Content.ReadAsAsync<Dictionary<string, object>>();
        //var accessToken = data["access_token"].ToString();

        //var octo = new GitHubClient(new Octokit.ProductHeaderValue("Devlooped-Sponsor", new Version(ThisAssembly.Info.Version).ToString(2)))
        //{
        //    Credentials = new Credentials(accessToken)
        //};

        //var emails = await octo.User.Email.GetAll();
        //var verified = emails.Where(x => x.Verified && !x.Email.EndsWith("@users.noreply.github.com")).Select(x => x.Email).ToArray();
        //var user = await octo.User.Current();
        
        //await users.PutAsync(new User(user.Id, user.Login, user.Email, accessToken!));

        //foreach (var email in verified)
        //    await this.emails.PutAsync(new EmailUser(email, user.Id));

        // If user is already a sponsor, go to thanks
        // otherwise, go to sponsorship page

        return new RedirectResult("https://devlooped.com/sponsors");
    }

    [FunctionName("apphook")]
    public async Task<IActionResult> AppHookAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/{kind}")] HttpRequest req, string kind)
    {
        using var reader = new StreamReader(req.Body);
        dynamic? payload = new Newtonsoft.Json.JsonSerializer().Deserialize(new JsonTextReader(reader));

        if (payload == null)
            return new BadRequestObjectResult("Could not deserialize payload as JSON");

        string action = payload.action;
        var note = $"{payload.installation.account.login} by {payload.sender.login}";
        var appKind = Enum.Parse<AppKind>(kind);

        if (action == "created")
        {
            await manager.InstallAsync(appKind, (string)payload.installation.account.id, note);
        }
        else if (action == "deleted")
        {
            await manager.UninstallAsync(appKind, (string)payload.installation.account.id, note);
        }

        return new OkObjectResult($"{payload.installation.account.login} by {payload.sender.login}");
    }

    [FunctionName("sponsorhook")]
    public async Task<IActionResult> SponsorHookAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sponsors/{account}")] HttpRequest req, string account)
    {
        using var reader = new StreamReader(req.Body);
        dynamic? payload = new Newtonsoft.Json.JsonSerializer().Deserialize(new JsonTextReader(reader));

        if (payload == null)
            return new BadRequestObjectResult("Could not deserialize payload as JSON");

        string action = payload.action;
        string sponsorableId = payload.sponsorable.id;
        string sponsorableLogin = payload.sponsorable.login;
        string sponsorId = payload.sponsor.id;
        string sponsorLogin = payload.sponsor.login;
        int amount = payload.sponsorship.tier.monthly_price_in_dollars;
        bool oneTime = payload.sponsorship.tier.is_one_time;
        DateTime date = payload.sponsorship.created_at;

        if (action == "created")
        {
            var note = $"{sponsorLogin} > {sponsorableLogin} : ${amount}";
            if (oneTime)
                note += " (one-time)";

            await manager.SponsorAsync(sponsorableId, sponsorId, amount, oneTime ? DateOnly.FromDateTime(date).AddDays(30) : null, note);
        }
        else if (action == "pending_cancellation")
        {
            DateTime cancelAt = payload.effective_date;
            await manager.UnsponsorAsync(sponsorableId, sponsorId, DateOnly.FromDateTime(cancelAt), 
                $"{sponsorLogin} x {sponsorableLogin} by ${DateOnly.FromDateTime(cancelAt)}");
        }
        else if (action == "tier_changed")
        {
            int from = payload.changes.tier.from.monthly_price_in_dollars;
            var note = $"{sponsorLogin} > {sponsorableLogin} : ${from} > ${amount}";

            await manager.SponsorUpdateAsync(sponsorableId, sponsorId, amount, note);
        }

        // TODO: if sponsorable has not installed the admin app or is 
        // not sponsoring devlooped with at least $x, return OK with content explaining 
        // this and that checks won't work until they do.


        //var body = await new StreamReader(req.Body).ReadToEndAsync();
        //var json = JToken.Parse(body).ToString(Formatting.Indented);

        //var id = req.Headers["X-GitHub-Delivery"].ToString();
        //// The delivery identifier is the most precise. But if we happen to not get the 
        //// webhook from GH, we just generate a unique identifier based on the payload itself
        //// This avoids duplicating entries even from retries.
        //if (string.IsNullOrEmpty(id))
        //    id = Base62.Encode(BigInteger.Abs(new BigInteger(SHA1.HashData(Encoding.UTF8.GetBytes(body)))));

        //var type = req.Headers["X-GitHub-Event"].ToString();
        //if (string.IsNullOrEmpty(type))
        //    type = "general";

        //subject ??= "general";

        //await repository.PutAsync(new TableEntity($"{topic}.{subject}", $"{type}.{id}")
        //{
        //    ["body"] = json
        //});

        //var key = configuration["EventGridAccessKey"];
        //var domain = configuration["EventGridDomain"];
        //if (key != null && domain != null)
        //{
        //    using var client = new EventGridClient(new TopicCredentials(key));
        //    await client.PublishEventsAsync(domain, new List<EventGridEvent>
        //    {
        //        new EventGridEvent(
        //            id: id,
        //            subject: subject,
        //            topic: topic,
        //            data: json,
        //            eventType: type,
        //            eventTime: DateTime.UtcNow,
        //            dataVersion: "1.0")
        //    });
        //}

        return new OkResult();
    }

    [FunctionName("sponsor")]
    public async Task Sponsors([EventGridTrigger] EventGridEvent e)
    {
        // Event path is: webhook/[topic:devlooped]/[subject:sponsors]/[event:sponsorship]
        // As configured on the dashboard at https://github.com/sponsors/devlooped/dashboard/webhooks/396006910/edit
        if (!e.Topic.EndsWith("/devlooped") || 
            e.EventType != "sponsorship" || 
            e.Subject != "sponsors")
            return;

        using var reader = new StreamReader(e.Data.ToStream());
        var payload = await reader.ReadToEndAsync();

        File.WriteAllText($"sponsor-{DateTimeOffset.Now.ToQuaranTimeSeconds()}.json", payload);
    }

    //[FunctionName("sponsor")]
    //public static async Task<IActionResult> Sponsors(
    //    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
    //{
    //    using var reader = new StreamReader(req.Body);
    //    var payload = await reader.ReadToEndAsync();

    //    File.WriteAllText($"sponsor-{DateTimeOffset.Now.ToQuaranTimeSeconds()}.json", payload);

    //    return new OkResult();
    //}
}
