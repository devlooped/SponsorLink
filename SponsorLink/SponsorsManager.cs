using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Octokit;

namespace Devlooped.SponsorLink;

#pragma warning disable CS1998

[Service]
public class SponsorsManager
{
    static readonly ProductInfoHeaderValue httpProduct = new ProductInfoHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2));
    static readonly Octokit.ProductHeaderValue octoProduct = new Octokit.ProductHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2));

    readonly IHttpClientFactory httpFactory;
    readonly SecurityManager security;
    readonly CloudStorageAccount storageAccount;
    readonly TableConnection tableConnection;
    readonly IEventStream events;

    public SponsorsManager(IHttpClientFactory httpFactory, SecurityManager security, CloudStorageAccount storageAccount, TableConnection tableConnection, IEventStream events)
        => (this.httpFactory, this.security, this.storageAccount, this.tableConnection, this.events) 
        = (httpFactory, security, storageAccount, tableConnection, events);

    public async Task AuthorizeAsync(AppKind kind, long installation, string code)
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
        var id = new AccountId(user.Id, user.NodeId, user.Login);
        var partition = DocumentPartition.Create<Account>(tableConnection,
            kind == AppKind.Sponsorable ? "Sponsorable" : "Sponsor", includeProperties: true);

        var account = await partition.GetAsync(user.Id.ToString());
        // This might happen if the processing of the app install somehow didn't finish 
        // before we do, which is highly unlikely
        if (account == null)
            // Note we infer an app install at this point, since there's no other way we got 
            // to the authorize without an app install
            account = new Account(id, AppState.Installed, true);

        await partition.PutAsync(account with
        {
            AccessToken = accessToken,
            Authorized = true,
        });
    }

    public async Task RefreshAccountAsync(AccountId account)
    {
        // TODO: cache/externalize as table connection?
        var byEmail = DocumentRepository.Create<AccountEmail>(
            tableConnection.StorageAccount,
            partitionKey: x => x.Email,
            rowKey: x => x.AccountId,
            includeProperties: true);

        var byAccount = DocumentRepository.Create<AccountEmail>(
            tableConnection.StorageAccount,
            partitionKey: x => x.Email,
            rowKey: x => x.AccountId,
            includeProperties: true);

        // We refresh emails for both sponsorable and sponsor accounts with the given 
        // user id, since we want to also verify sponsorships from sponsorable accounts.
        var partition = DocumentPartition.Create<Account>(tableConnection, "Sponsorable");

        var entity = await partition.GetAsync(account.Id.ToString());
        if (entity?.AccessToken != null)
        {
            var octo = new GitHubClient(octoProduct)
            {
                Credentials = new Credentials(entity.AccessToken)
            };

            var allmail = await octo.User.Email.GetAll();
            var verified = allmail.Where(x => x.Verified && !x.Email.EndsWith("@users.noreply.github.com"))
                .Select(x => new AccountEmail(account, x.Email)).ToArray();

            // Allows locating by email (all accounts, there may be more than one?)
            // and by account (fetch all emails for it).
            foreach (var email in verified)
            {
                await byEmail.PutAsync(email);
                await byAccount.PutAsync(email);
            }
        }
    }

    public async Task AppInstallAsync(AppKind kind, AccountId account, string? note = default)
    {
        var partition = DocumentPartition.Create<Account>(tableConnection, 
            kind == AppKind.Sponsorable ? "Sponsorable" : "Sponsor", includeProperties: true);

        await partition.PutAsync(new Account(account, AppState.Installed, false));
        await events.PushAsync(new AppInstalled(account, kind, note));
    }

    public async Task AppSuspendAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Suspended, note);
        await events.PushAsync(new AppSuspended(account, kind, note));
    }

    public async Task AppUnsuspendAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Installed, note);
        await events.PushAsync(new AppSuspended(account, kind, note));
    }

    public async Task AppUninstallAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Deleted, note);
    }

    public async Task SponsorAsync(AccountId sponsorable, AccountId sponsor, int amount, DateOnly? expiresAt = null, string? note = default)
    {
        
    }

    public async Task SponsorUpdateAsync(AccountId sponsorable, AccountId sponsor, int amount, string? note = default)
    {
    }

    public async Task UnsponsorAsync(AccountId sponsorable, AccountId sponsor, DateOnly cancelAt, string? note = default)
    {
        
    }

    public async Task CheckExpirationsAsync()
    {
    }

    async Task ChangeState(AppKind kind, AccountId account, AppState state, string? note)
    {
        var partition = DocumentPartition.Create<Account>(tableConnection,
            kind == AppKind.Sponsorable ? "Sponsorable" : "Sponsor", includeProperties: true);

        var installed = await partition.GetAsync(account.Id.ToString());

        if (installed == null)
            throw new ArgumentException($"{kind} app is not installed for account {account.Login}.");

        installed = installed with { State = state };

        await partition.PutAsync(installed);
    }
}
