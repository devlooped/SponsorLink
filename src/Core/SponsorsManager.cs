using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpYaml.Serialization;

namespace Devlooped.Sponsors;

public class SponsorsManager(IConfiguration configuration, IHttpClientFactory httpFactory, IMemoryCache cache, ILogger<SponsorsManager> logger)
{
    static readonly Serializer serializer = new(new SerializerSettings
    {
        NamingConvention = new CamelCaseNamingConvention()
    });

    public async Task<SponsorableManifest> GetManifestAsync()
    {
        if (!cache.TryGetValue<SponsorableManifest>(typeof(SponsorableManifest), out var manifest) || manifest is null)
        {
            // Populate manifest
            if (configuration["SPONSORLINK_ACCOUNT"] is not { Length: > 0 } account)
            {
                // Auto-discovery by fetching from [user]/.github/sponsorlink.yml
                using var gh = httpFactory.CreateClient("sponsorable");
                account = await gh.QueryAsync(GraphQueries.ViewerLogin);

                logger.Assert(account is { Length: > 0 }, "Failed to determine sponsorable user from configured GH_TOKEN");
            }

            var url = $"https://github.com/{account}/.github/raw/main/sponsorlink.yml";

            using var http = httpFactory.CreateClient("sponsorable");
            var response = await http.GetAsync(url);

            logger.Assert(response.IsSuccessStatusCode, 
                "Failed to retrieve manifest from {Url}: {StatusCode} {Reason}", 
                url, (int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var yaml = await response.Content.ReadAsStringAsync();
            manifest = serializer.Deserialize<SponsorableManifest>(yaml);

            logger.Assert(manifest is not null, 
                "Failed to deserialize YAML manifest from {Url}", url);

            // Audience defaults to the manifest url user/org
            manifest.Audience ??= new Uri(url).Segments[1].Trim('/');

            // Set the account type
            var sponsorable = await http.QueryAsync<Sponsorable>(GraphQueries.Sponsorable(manifest.Audience));
            // If we could retrieve the manifest, we can assume the account is valid
            manifest.AccountType = sponsorable!.Type;

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

        // Use the sponsorable token since it has access to sponsorship info even if it's private?
        var sponsoring = await sponsorable.QueryAsync<string[]>(GraphQueries.IsSponsoredBy(manifest.Audience, manifest.AccountType, logins));
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

        var contribs = await sponsor.QueryAsync<string[]>(GraphQueries.UserContributions);
        if (contribs is not null && 
            contribs.Contains(manifest.Audience))
        {
            return SponsorType.Contributor;
        }

        return SponsorType.None;
    }
}
