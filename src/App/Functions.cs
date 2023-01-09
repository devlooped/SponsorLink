using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Devlooped;
using JWT.Algorithms;
using JWT.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace App;

public class Functions
{
    readonly IHttpClientFactory clientFactory;
    readonly ITablePartition<User> users;
    readonly ITablePartition<UserEmail> emails;

    public Functions(IHttpClientFactory clientFactory, ITablePartition<User> users, ITablePartition<UserEmail> emails)
        => (this.clientFactory, this.users, this.emails) 
        = (clientFactory, users, emails);

    record EmailInfo(string email, bool verified);

    [FunctionName("authorize")]
    public async Task<IActionResult> AuthorizeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        var key = RSA.Create();
        key.ImportFromPem(ThisAssembly.Resources.privatekey.Text);

        var jwt = JwtBuilder.Create()
                  .WithAlgorithm(new RS256Algorithm(key, key))
                  .Issuer("279204")
                  .AddClaim("iat", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds())
                  .AddClaim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds())
                  .Encode();

        var code = req.Query["code"].ToString();
        var installation = req.Query["installation_id"].ToString();
        var action = req.Query["setup_action"].ToString();

        var apireq = new HttpRequestMessage(HttpMethod.Post, $"https://github.com/login/oauth/access_token");
        apireq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {jwt}");
        apireq.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        apireq.Headers.UserAgent.Add(new ProductInfoHeaderValue("Devlooped-Sponsors", new Version(ThisAssembly.Info.Version).ToString(2)));
        apireq.Content = new StringContent(JsonSerializer.Serialize(new
        {
            client_id = "Iv1.e377fc2cb3d5ea8c",
            client_secret = "78c98db8586305eb3f533a857687ff9aa86c7dee",
            code,
            redirect_uri = "https://d428-190-192-227-251.sa.ngrok.io/authorize"
        }), Encoding.UTF8, "application/json");

        using var http = clientFactory.CreateClient();
        var resp = await http.SendAsync(apireq);
        var data = await resp.Content.ReadAsAsync<Dictionary<string,object>>();
        var accessToken = data["access_token"].ToString();

        var emailreq = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/user/emails");
        emailreq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        emailreq.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        emailreq.Headers.UserAgent.Add(new ProductInfoHeaderValue("Devlooped-Sponsors", new Version(ThisAssembly.Info.Version).ToString(2)));

        resp = await http.SendAsync(emailreq);
        var emails = await resp.Content.ReadAsAsync<EmailInfo[]>();
        var verified = emails.Where(x => x.verified && !x.email.EndsWith("@users.noreply.github.com")).Select(x => x.email).ToArray();

        var userreq = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/user");
        userreq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        userreq.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        userreq.Headers.UserAgent.Add(new ProductInfoHeaderValue("Devlooped-Sponsors", new Version(ThisAssembly.Info.Version).ToString(2)));

        resp = await http.SendAsync(userreq);
        data = await resp.Content.ReadAsAsync<Dictionary<string, object>>();
        var login = data["login"].ToString();

        await users.PutAsync(new User(login!, accessToken!));

        foreach (var email in verified)
            await this.emails.PutAsync(new UserEmail(email!, login!));

        return new RedirectResult("https://devlooped.com/sponsors");
    }

    [FunctionName("webhook")]
    public static async Task<IActionResult> Webhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        using var reader = new StreamReader(req.Body);
        var payload = await reader.ReadToEndAsync();

        File.WriteAllText("webhook.json", payload);

        return new RedirectResult("https://devlooped.com/sponsors");
    }
}
