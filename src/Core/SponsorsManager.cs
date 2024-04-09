using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Devlooped.Sponsors;

public class SponsorsManager(IConfiguration configuration, IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<SponsorsManager> logger)
{
    public async Task<SponsorableManifest> GetManifestAsync()
    {
        if (!cache.TryGetValue<SponsorableManifest>(typeof(SponsorableManifest), out var manifest) || manifest is null)
        {
            // Populate manifest
            if (configuration["SponsorLink:Account"] is not { Length: > 0 } account)
            {
                // Auto-discovery by fetching from [user]/.github/sponsorlink.jwt
                using var gh = httpFactory.CreateClient("sponsorable");
                account = await gh.QueryAsync(GraphQueries.ViewerLogin);

                logger.Assert(account?.Length > 0, "Failed to determine sponsorable user from configured GitHub token.");
            }

            var url = $"https://github.com/{account}/.github/raw/main/sponsorlink.jwt";

            using var http = httpFactory.CreateClient("sponsorable");
            var response = await http.GetAsync(url);

            logger.Assert(response.IsSuccessStatusCode,
                "Failed to retrieve manifest from {Url}: {StatusCode} {Reason}",
                url, (int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var jwt = await response.Content.ReadAsStringAsync();
            manifest = SponsorableManifest.FromJwt(jwt);

            var audience = manifest.Audience;

            // Set the account type
            if (Uri.TryCreate(manifest.Audience, UriKind.Absolute, out var audienceUri))
                audience = audienceUri.Segments[^1].TrimEnd('/');

            var sponsorable = await http.QueryAsync<Sponsorable>(GraphQueries.Sponsorable(audience));
            // If we could retrieve the manifest, we can assume the account is valid
            //manifest.AccountType = sponsorable!.Type;

            manifest = cache.Set(typeof(SponsorableManifest), manifest, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
        }

        return manifest;
    }

    /// <summary>
    /// Gets the list of accounts related to the authenticated user, 
    /// that are sponsoring the sponsorable account. These can be the 
    /// current user or any organization he belongs to.
    /// </summary>
    public async Task<SponsorType> GetSponsorAsync()
    {
        if (ClaimsPrincipal.Current is not { Identity.IsAuthenticated: true } principal)
            return SponsorType.None;

        // This uses the current authenticated user to query GH API
        using var sponsor = httpFactory.CreateClient("sponsor");
        using var sponsorable = httpFactory.CreateClient("sponsorable");

        var logins = await sponsor.QueryAsync<string[]>(GraphQueries.UserSponsorCandidates);
        logger.Assert(logins is not null, "Failed to retrieve user sponsor candidates");

        var manifest = await GetManifestAsync();
        logger.Assert(manifest is not null, "Failed to retrieve sponsorable manifest");

        var audience = manifest.Audience;

        // Set the account type
        if (Uri.TryCreate(manifest.Audience, UriKind.Absolute, out var audienceUri))
            audience = audienceUri.Segments[^1].TrimEnd('/');

        // Use the sponsorable token since it has access to sponsorship info even if it's private
        // TODO:
        var sponsoring = await sponsorable.QueryAsync<string[]>(GraphQueries.IsSponsoredBy(audience, AccountType.Organization, logins));
        var accounts = new HashSet<string>(sponsoring ?? []);

        // User is checked for auth on first line above
        if (principal.FindFirst("urn:github:login") is { Value.Length: > 0 } claim &&
            accounts.Contains(claim.Value))
        {
            // the user is directly sponsoring
            return SponsorType.User;
        }

        if (accounts.Count > 0)
            return SponsorType.Organization;

        // TODO: add verified org email(s) > user's emails check (even if user's email is not public 
        // and the logged in account does not belong to the org). This covers the scenario where a 
        // user has multiple GH accounts, one for each org he works for (i.e. a consultant), and a 
        // personal account. The personal account would not be otherwise associated with any of his 
        // client's orgs, but he could still add his work emails to his personal account, keep them 
        // private and verified, and then use them to access and be considered an org sponsor.

        var contribs = await sponsor.QueryAsync<string[]>(GraphQueries.ViewerContributions);
        if (contribs is not null &&
            contribs.Contains(manifest.Audience))
        {
            return SponsorType.Contributor;
        }

        return SponsorType.None;
    }
}
