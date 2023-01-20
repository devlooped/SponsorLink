using System.Net.Http.Headers;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
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
    readonly SponsorsRegistry registry;
    readonly TableConnection sponsorshipsConnection;

    public SponsorsManager(
        IHttpClientFactory httpFactory, SecurityManager security, CloudStorageAccount storageAccount,
        TableConnection tableConnection, IEventStream events, SponsorsRegistry registry)
        => (this.httpFactory, this.security, this.storageAccount, this.tableConnection, this.events, this.registry, sponsorshipsConnection)
        = (httpFactory, security, storageAccount, tableConnection, events, registry, new TableConnection(storageAccount, nameof(Sponsorship)));

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

        try
        {
            string accessToken = data.access_token;
            var octo = new GitHubClient(octoProduct)
            {
                Credentials = new Credentials(accessToken)
            };

            var user = await octo.User.Current();
            var partition = TablePartition.Create<Authorization>(tableConnection);

            await partition.PutAsync(new Authorization(user.NodeId, accessToken, user.Login));
            await events.PushAsync(new UserAuthorized(user.NodeId, user.Login, kind));
        }
        catch (RuntimeBinderException)
        {
            throw new ArgumentException("Invalid authorization code", nameof(code));
        }
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

    public Task<Installation?> FindAppAsync(AppKind kind, AccountId account)
    {
        var partition = TablePartition.Create<Installation>(tableConnection,
            kind == AppKind.Sponsorable ? "Sponsorable" : "Sponsor");

        return partition.GetAsync(account.Id);
    }

    public async Task AppInstallAsync(AppKind kind, AccountId account, string? note = default)
    {
        var partition = TablePartition.Create<Installation>(tableConnection,
            kind == AppKind.Sponsorable ? "Sponsorable" : "Sponsor");

        var installation = new Installation(account.Id, account.Login, AppState.Installed, Guid.NewGuid().ToString());

        await partition.PutAsync(installation);
        await events.PushAsync(new AppInstalled(account.Id, account.Login, kind, note));
    }

    public async Task AppSuspendAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Suspended);
        await events.PushAsync(new AppSuspended(account.Id, account.Login, kind, note));
    }

    public async Task AppUnsuspendAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Installed);
        await events.PushAsync(new AppSuspended(account.Id, account.Login, kind, note));
    }

    public async Task AppUninstallAsync(AppKind kind, AccountId account, string? note = default)
    {
        await ChangeState(kind, account, AppState.Deleted);
        await events.PushAsync(new AppUninstalled(account.Id, account.Login, kind, note));
    }

    public async Task SponsorAsync(AccountId sponsorable, AccountId sponsor, int amount, DateOnly? expiresAt = null, string? note = default)
    {
        var sponsorship = new Sponsorship(sponsorable.Id, sponsorable.Login, sponsor.Id, sponsor.Login, amount)
        {
            ExpiresAt = expiresAt
        };

        await StoreSponsorshipAsync(sponsorship);
        await events.PushAsync(new SponsorshipCreated(sponsorable.Id, sponsor.Id, amount, expiresAt, note));

        // NOTE: we persist above so a quick Enable/Disable of the admin app can successfully 
        // re-establish all current sponsors.
        await VerifySponsorableAsync(sponsorable);
        await UpdateRegistryAsync(sponsorable, sponsor);
    }

    public async Task SponsorUpdateAsync(AccountId sponsorable, AccountId sponsor, int amount, string? note = default)
    {
        var sponsorship = new Sponsorship(sponsorable.Id, sponsorable.Login, sponsor.Id, sponsor.Login, amount);

        // If there is an existing one, this should merge/update. 
        // Otherwise, this will create a new entry. This can be used to 
        // "refresh" an existing sponsor from before the app was installed.
        await StoreSponsorshipAsync(sponsorship);
        await events.PushAsync(new SponsorshipChanged(sponsorable.Id, sponsor.Id, amount, note));

        // NOTE: we persist above so a quick Enable/Disable of the admin app can successfully 
        // re-establish all current sponsors.
        await VerifySponsorableAsync(sponsorable);
        await UpdateRegistryAsync(sponsorable, sponsor);
    }

    public async Task UnsponsorAsync(AccountId sponsorable, AccountId sponsor, string? note = default)
    {
        var bySponsorable = TableRepository
            .Create<Sponsorship>(sponsorshipsConnection, x => x.SponsorableId, x => x.SponsorId);

        var sponsorship = await bySponsorable.GetAsync(sponsorable.Id, sponsor.Id);
        if (sponsorship == null)
            // TODO: We have no existing sponsorship to expire anyway, so ignore?
            return;

        var bySponsor = TableRepository
            .Create<Sponsorship>(sponsorshipsConnection, x => x.SponsorId, x => x.SponsorableId);

        sponsorship = await bySponsor.GetAsync(sponsor.Id, sponsorable.Id);
        if (sponsorship == null)
            // TODO: We have no existing sponsorship to expire anyway, so ignore?
            return;

        sponsorship = sponsorship with
        {
            Expired = true,
            ExpiresAt = DateOnly.FromDateTime(DateTime.Today),
        };

        await StoreSponsorshipAsync(sponsorship);
        await events.PushAsync(new SponsorshipCancelled(sponsorable.Id, sponsor.Id, note));

        // This means sponsorables need to be active to also *unregister* sponsors
        await VerifySponsorableAsync(sponsorable);

        await registry.UnregisterSponsorAsync(sponsorable, sponsor);
    }

    async Task VerifySponsorableAsync(AccountId sponsorable)
    {
        // We're always authorized to ourselves.
        if (sponsorable.Id == Constants.DevloopedId)
            return;

        var app = await FindAppAsync(AppKind.Sponsorable, sponsorable);
        if (app == null || app.State == AppState.Deleted)
            throw new ArgumentException($"No SponsorLink Admin installation found for {sponsorable.Login}.", nameof(sponsorable));

        if (app.State == AppState.Suspended)
            throw new ArgumentException($"SponsorLink Admin app was suspended by the {sponsorable.Login} account.", nameof(sponsorable));

        var sponsorship = await TablePartition.Create<Sponsorship>(sponsorshipsConnection, $"Sponsorable-{sponsorable.Id}", x => x.SponsorId)
            .GetAsync(Constants.DevloopedId);

        if (sponsorship == null ||
            (sponsorship.ExpiresAt != null && sponsorship.ExpiresAt < DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new ArgumentException($"SponsorLink usage requires an active sponsorship from {sponsorable.Login} to @{Constants.DevloopedLogin}.", nameof(sponsorable));
    }

    async Task StoreSponsorshipAsync(Sponsorship sponsorship)
    {
        // Dual store for easier scanning
        var bySponsorable = TablePartition.Create<Sponsorship>(sponsorshipsConnection, 
            $"Sponsorable-{sponsorship.SponsorableId}", x => x.SponsorId, Azure.Data.Tables.TableUpdateMode.Replace);
        var bySponsor = TablePartition.Create<Sponsorship>(sponsorshipsConnection,
            $"Sponsor-{sponsorship.SponsorId}", x => x.SponsorableId, Azure.Data.Tables.TableUpdateMode.Replace);

        var expirations = TableRepository.Create(sponsorshipsConnection);

        // first retrieve existing, to see if there was a previous scheduled expiration 
        // from a prior one-time sponsorship that is being turned into a monthly one
        var existing = await bySponsorable.GetAsync(sponsorship.SponsorId);
        if (existing?.ExpiresAt != null)
        {
            // Delete existing expiration record if present
            await expirations.DeleteAsync($"Expiration-{existing.ExpiresAt:O}", $"{sponsorship.SponsorableId}|{sponsorship.SponsorId}");
        }

        if (sponsorship.ExpiresAt != null)
        {
            // Schedule expiration for the daily check to pick up.
            await expirations.PutAsync(new($"Expiration-{sponsorship.ExpiresAt:O}", $"{sponsorship.SponsorableId}|{sponsorship.SponsorId}"));
        }

        // NOTE: we *replace* existing expiration if it was present.
        await bySponsorable.PutAsync(sponsorship);
        await bySponsor.PutAsync(sponsorship);
    }

    async Task UpdateRegistryAsync(AccountId sponsorable, AccountId sponsor)
    {
        var app = await FindAppAsync(AppKind.Sponsor, sponsor);
        if (app == null || app.State != AppState.Installed)
            // TODO: new sponsor but no app installed (or suspended)... should be fine?
            // This is basically a sponsor that doesn't necessarily need or use the library
            return;

        var auth = await TablePartition.Create<Authorization>(tableConnection)
            .GetAsync(sponsor.Id);

        // Should be fine too if they cancelled somehow before auth completed?
        if (auth == null)
            return;

        var emails = await new GitHubClient(octoProduct)
        {
            Credentials = new Credentials(auth.AccessToken)
        }.User.Email.GetAll();

        await registry.RegisterSponsorAsync(
            sponsorable, sponsor,
            emails.Where(x => x.Verified && !x.Email.EndsWith("@users.noreply.github.com")).Select(x => x.Email));
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
