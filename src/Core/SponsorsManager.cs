using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpYaml.Serialization;

namespace Devlooped.Sponsors;

public partial class SponsorsManager(
    IOptions<SponsorLinkOptions> options, IHttpClientFactory httpFactory,
    IGraphQueryClientFactory graphFactory,
    IMemoryCache cache, ILogger<SponsorsManager> logger)
{
    static readonly Serializer serializer = new();
    SponsorLinkOptions options = options.Value;

    public async Task<SponsorableManifest> GetManifestAsync()
    {
        if (!cache.TryGetValue<SponsorableManifest>(typeof(SponsorableManifest), out var manifest) || manifest is null)
        {
            var client = graphFactory.CreateClient("sponsorable");

            var account = string.IsNullOrEmpty(options.Account) ?
                // default to the authenticated user login
                await client.QueryAsync(GraphQueries.ViewerAccount)
                    ?? throw new ArgumentException("Failed to determine sponsorable user from configured GitHub token.") :
                await client.QueryAsync(GraphQueries.FindOrganization(options.Account))
                    ?? await client.QueryAsync(GraphQueries.FindUser(options.Account))
                    ?? throw new ArgumentException("Failed to determine sponsorable user from configured GitHub token.");

            var url = $"https://github.com/{account.Login}/.github/raw/main/sponsorlink.jwt";

            // Manifest should be public, so no need for any special HTTP client.
            using var http = httpFactory.CreateClient();
            var response = await http.GetAsync(url);

            logger.Assert(response.IsSuccessStatusCode,
                "Failed to retrieve manifest from {Url}: {StatusCode} {Reason}",
                url, (int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var jwt = await response.Content.ReadAsStringAsync();
            logger.Assert(SponsorableManifest.TryRead(jwt, out manifest, out var missing),
                "Failed to read manifest due to missing required claim '{0}'", missing);

            var audience = manifest.Audience;
            if (Uri.TryCreate(manifest.Audience, UriKind.Absolute, out var audienceUri))
                audience = audienceUri.Segments[^1].TrimEnd('/');

            // Manifest audience should match the sponsorable account to avoid weird issues?
            if (account.Login != audience)
                throw new InvalidOperationException("Manifest audience does not match configured sponsorable account.");

            manifest = cache.Set(typeof(SponsorableManifest), manifest, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
        }

        return manifest;
    }

    /// <summary>
    /// Direct or indirect sponsorship types for the the authenticated user.
    /// </summary>
    public async Task<SponsorTypes> GetSponsorTypeAsync(ClaimsPrincipal? principal = default)
    {
        principal ??= ClaimsPrincipal.Current;

        if (principal is not { Identity.IsAuthenticated: true })
            return SponsorTypes.None;

        var sponsor = graphFactory.CreateClient("sponsor");
        var sponsorable = graphFactory.CreateClient("sponsorable");

        var type = SponsorTypes.None;

        // This uses the current authenticated user to query GH API
        var logins = await sponsor.QueryAsync(GraphQueries.ViewerSponsorableCandidates);
        logger.Assert(logins is not null, "Failed to retrieve user sponsor candidates");

        var manifest = await GetManifestAsync();
        logger.Assert(manifest is not null, "Failed to retrieve sponsorable manifest");

        var audience = manifest.Audience;

        // Set the account type
        if (Uri.TryCreate(manifest.Audience, UriKind.Absolute, out var audienceUri))
            audience = audienceUri.Segments[^1].TrimEnd('/');

        var account = await sponsorable.QueryAsync(GraphQueries.Sponsorable(audience));
        logger.Assert(account is not null, "Failed to retrieve sponsorable account");

        if (logins.Contains(account.Login))
            type |= SponsorTypes.Team;

        // Use the sponsorable token since it has access to sponsorship info even if it's private
        var sponsoring = await sponsorable.QueryAsync(GraphQueries.IsSponsoredBy(audience, logins));
        logger.Assert(sponsoring is not null);

        // User is checked for auth on first line above
        if (principal.FindFirst("urn:github:login") is { Value.Length: > 0 } claim &&
            sponsoring.Contains(claim.Value))
        {
            // the user is directly sponsoring
            type |= SponsorTypes.User;
        }
        else if (sponsoring.Length > 0)
        {
            // the user is a member of a sponsoring organization
            // note that both could be true at the same time, yet 
            // we consider user sponsoring to be a higher priority
            // and we won't therefore have both User and Organization 
            // for any user.
            type |= SponsorTypes.Organization;
        }

        // Next we check for direct contributions too. 
        // TODO: should this be configurable?
        var contribs = await sponsor.QueryAsync(GraphQueries.ViewerOwnerContributions);
        if (contribs is not null &&
            contribs.Contains(manifest.Audience))
        {
            type |= SponsorTypes.Contributor;
        }

        // Add verified org email(s) > user's emails check (even if user's email is not public 
        // and the logged in account does not belong to the org). This covers the scenario where a 
        // user has multiple GH accounts, one for each org he works for (i.e. a consultant), and a 
        // personal account. The personal account would not be otherwise associated with any of his 
        // client's orgs, but he could still add his work emails to his personal account, keep them 
        // private and verified, and then use them to access and be considered an org sponsor.

        // Only do this if we couldn't already determine if the user is a sponsor (directly or indirectly), 
        // since it's expensive.
        if (type != SponsorTypes.None)
            return type;

        if (!cache.TryGetValue<Organization[]>(typeof(Organization[]), out var sponsoringOrgs) || sponsoringOrgs is null)
        {
            sponsoringOrgs = account.Type == AccountType.User ? 
                await sponsorable.QueryAsync(GraphQueries.SponsoringOrganizationsForUser(account.Login)) :
                await sponsorable.QueryAsync(GraphQueries.SponsoringOrganizationsForOrg(account.Login));

            cache.Set(typeof(Organization[]), sponsoringOrgs, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
        }

        if (sponsoringOrgs?.Length > 0)
        {
            var domains = new HashSet<string>();

            // Collect unique domains from verified org website and email
            foreach (var org in sponsoringOrgs)
            {
                if (Uri.TryCreate(org.WebsiteUrl, UriKind.Absolute, out var uri))
                    domains.Add(uri.Host);

                if (string.IsNullOrEmpty(org.Email))
                    continue;

                var domain = org.Email.Split('@')[1];
                if (!string.IsNullOrEmpty(domain))
                    domains.Add(domain);
            }

            var emails = await sponsor.QueryAsync(GraphQueries.ViewerEmails);
            if (emails?.Length > 0)
            {
                foreach (var email in emails)
                {
                    var domain = email.Split('@')[1];
                    if (domains.Contains(domain))
                    {
                        type |= SponsorTypes.Organization;
                        // One is enough.
                        break;
                    }
                }
            }
        }

        return type;
    }

    public async Task<List<Claim>?> GetSponsorClaimsAsync(ClaimsPrincipal? principal = default)
    {
        principal ??= ClaimsPrincipal.Current;
        var manifest = await GetManifestAsync();

        var sponsor = await GetSponsorTypeAsync(principal);
        if (sponsor == SponsorTypes.None ||
            principal?.FindFirst("urn:github:login")?.Value is not string id)
            return null;

        var claims = new List<Claim>
        {
            new("iss", manifest.Issuer),
            new("aud", manifest.Audience),
            new("client_id", manifest.ClientId),
            new("sub", id),
        };

        // check for each flags SponsorTypes and add claims accordingly
        if (sponsor.HasFlag(SponsorTypes.Team))
            claims.Add(new("role", "team"));
        if (sponsor.HasFlag(SponsorTypes.Organization))
            claims.Add(new("role", "org"));
        if (sponsor.HasFlag(SponsorTypes.User))
            claims.Add(new("role", "user"));
        if (sponsor.HasFlag(SponsorTypes.Contributor))
            claims.Add(new("role", "contrib"));

        // Use shorthand JWT claim for emails. See https://www.iana.org/assignments/jwt/jwt.xhtml
        claims.AddRange(principal.Claims.Where(x => x.Type == ClaimTypes.Email).Select(x => new Claim("email", x.Value)));

        return claims;
    }

    public async Task<List<Tier>> GetTiers()
    {
        if (!cache.TryGetValue<List<Tier>>(typeof(List<Tier>), out var tiers) || tiers is null)
        {
            var manifest = await GetManifestAsync();
            var client = graphFactory.CreateClient("sponsorable");
            tiers = [];

            var audience = manifest.Audience;
            if (Uri.TryCreate(manifest.Audience, UriKind.Absolute, out var audienceUri))
                audience = audienceUri.Segments[^1].TrimEnd('/');

            var json = await client.QueryAsync(GraphQueries.Tiers(audience));

            // TODO: should be an error?
            if (string.IsNullOrEmpty(json) ||
                JsonSerializer.Deserialize<RawTier[]>(json, JsonOptions.Default) is not { Length: > 0 } raw)
                return tiers;

            foreach (var item in raw)
            {
                var tier = new Tier(item.Name, YamlRegex().Replace(item.Description, ""), item.Amount, item.OneTime);
                var current = item;
                while (true)
                {
                    var yaml = YamlRegex().Match(current.Description)?.Groups["yaml"]?.Value;
                    if (!string.IsNullOrEmpty(yaml) &&
                        serializer.Deserialize<Dictionary<string, string>>(yaml) is { } meta)
                    {
                        foreach (var entry in meta)
                        {
                            // An existing value should not be overwritten
                            tier.Meta.TryAdd(entry.Key, entry.Value);
                        }
                    }

                    // Walk the tiers to aggregate metadata provided by lower tiers.
                    // This avoids having to repeat metadata in each tier.
                    if (string.IsNullOrEmpty(current.Previous) ||
                        raw.FirstOrDefault(x => x.Name == current.Previous) is not { } previous)
                        break;

                    current = previous;
                }
                tiers.Add(tier);
            }

            tiers = cache.Set(typeof(List<Tier>), tiers, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
        }

        return tiers;
    }

    record RawTier(string Name, string Description, int Amount, bool OneTime, string? Previous);

    [GeneratedRegex("<!--(?<yaml>.*)-->")]
    private static partial Regex YamlRegex();
}
