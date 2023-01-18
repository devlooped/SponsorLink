using System.Net.Http.Headers;
using System.Text;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Octokit;

namespace Devlooped.SponsorLink;

#pragma warning disable CS1998

[Service]
public class SponsorsManager
{
    static readonly ProductInfoHeaderValue httpProduct = new("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2));
    static readonly Octokit.ProductHeaderValue octoProduct = new("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2));

    readonly IHttpClientFactory httpFactory;
    readonly SecurityManager security;
    readonly CloudStorageAccount storageAccount;
    readonly TableConnection tableConnection;
    readonly IEventStream events;

    public SponsorsManager(IHttpClientFactory httpFactory, SecurityManager security, CloudStorageAccount storageAccount, TableConnection tableConnection, IEventStream events)
        => (this.httpFactory, this.security, this.storageAccount, this.tableConnection, this.events) 
        = (httpFactory, security, storageAccount, tableConnection, events);

    public async Task AuthorizeAsync(AppKind kind, string code)
    {
        var auth = security.CreateAuthorization(kind, code);
        var jwt = security.IssueToken(kind);

        using var http = httpFactory.CreateClient();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.UserAgent.Add(httpProduct);

        var resp = await http.PostAsync("https://github.com/login/oauth/access_token",
            new StringContent(auth, Encoding.UTF8, "application/json"), jwt);

        dynamic data = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync()) ?? 
            throw new InvalidOperationException("Failed to deserialize OAuth response as JSON.");
        
        string accessToken = data.access_token;
        var octo = new GitHubClient(octoProduct)
        {
            Credentials = new Credentials(accessToken)
        };

        var user = await octo.User.Current();
        var partition = TablePartition.Create<Authorization>(tableConnection);

        await partition.PutAsync(new Authorization(user.NodeId, accessToken, user.Login));
    }

    public async Task RefreshAccountAsync(AccountId account)
    {
        // TODO: cache/externalize as table connection?
        //var byEmail = DocumentRepository.Create<AccountEmail>(
        //    tableConnection.StorageAccount,
        //    partitionKey: x => x.Email,
        //    rowKey: x => x.Id.Id,
        //    includeProperties: true);

        //var byAccount = DocumentRepository.Create<AccountEmail>(
        //    tableConnection.StorageAccount,
        //    partitionKey: x => x.Id.Id,
        //    rowKey: x => x.Email,
        //    includeProperties: true);

        //// We refresh emails for both sponsorable and sponsor accounts with the given 
        //// user id, since we want to also verify sponsorships from sponsorable accounts.
        //var partition = DocumentPartition.Create<Installation>(tableConnection, "Sponsorable");

        //var entity = await partition.GetAsync(account.Id.ToString());
        //if (entity?.AccessToken != null)
        //{
        //    var octo = new GitHubClient(octoProduct)
        //    {
        //        Credentials = new Credentials(entity.AccessToken)
        //    };

        //    var allmail = await octo.User.Email.GetAll();
        //    var verified = allmail.Where(x => x.Verified && !x.Email.EndsWith("@users.noreply.github.com"))
        //        .Select(x => new AccountEmail(account, x.Email)).ToArray();

        //    // Allows locating by email (all accounts, there may be more than one?)
        //    // and by account (fetch all emails for it).
        //    foreach (var email in verified)
        //    {
        //        await byEmail.PutAsync(email);
        //        await byAccount.PutAsync(email);
        //    }
        //}
    }

    public async Task AppInstallAsync(AppKind kind, AccountId account, string? note = default)
    {
        var partition = TablePartition.Create<Installation>(tableConnection, 
            kind == AppKind.Sponsorable ? "Sponsorable" : "Sponsor");

        var installation = new Installation(account.Id, account.Login, AppState.Installed);
        
        await partition.PutAsync(installation);
        await events.PushAsync(new AppInstalled(account, kind, note));
    }

    public async Task AppSuspendAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Suspended);
        await events.PushAsync(new AppSuspended(account, kind, note));
    }

    public async Task AppUnsuspendAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Installed);
        await events.PushAsync(new AppSuspended(account, kind, note));
    }

    public async Task AppUninstallAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Deleted);
        await events.PushAsync(new AppUninstalled(account, kind, note));
    }

    public async Task SponsorAsync(AccountId sponsorable, AccountId sponsor, int amount, DateOnly? expiresAt = null, string? note = default)
    {
        
    }

    public async Task SponsorUpdateAsync(AccountId sponsorable, AccountId sponsor, int amount, string? note = default)
    {
    }

    public async Task UnsponsorAsync(AccountId sponsorable, AccountId sponsor, DateOnly expiresAt, string? note = default)
    {
        
    }

    public async Task CheckExpirationsAsync()
    {
    }

    async Task ChangeState(AppKind kind, AccountId account, AppState state)
    {
        var partition = TablePartition.Create<Installation>(tableConnection,
            kind == AppKind.Sponsorable ? "Sponsorable" : "Sponsor");

        var installed = await partition.GetAsync(account.Id);
        if (installed == null)
            throw new ArgumentException($"{kind} app is not installed for account {account.Login}.");

        await partition.PutAsync(installed with { State = state });
    }
}
