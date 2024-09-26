using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using SharpYaml.Serialization;

namespace Devlooped.Sponsors;

public partial class SponsorsManager(IOptions<SponsorLinkOptions> options,
    IHttpClientFactory httpFactory, IGraphQueryClientFactory graphFactory,
    IMemoryCache cache, AsyncLazy<OpenSource> oss, ILogger<SponsorsManager> logger)
{
    internal const string JwtCacheKey = nameof(SponsorsManager) + ".JWT";
    internal const string ManifestCacheKey = nameof(SponsorsManager) + ".Manifest";

    static readonly Serializer serializer = new();
    readonly SponsorLinkOptions options = options.Value;

    Dictionary<string, Sponsor>? sponsors;
    Dictionary<string, Tier>? tiers;

    public void RefreshSponsors() => sponsors = null;

    internal async Task<Dictionary<string, Sponsor>> GetSponsorsAsync()
    {
        if (sponsors is not null)
            return sponsors;

        var client = graphFactory.CreateClient("sponsorable");
        var account = await GetSponsorable(cache, client, options);
        var tiers = await GetTiersAsync();
        var raw = await client.QueryAsync(GraphQueries.Sponsors(account.Login));
        logger.Assert(raw != null, "Failed to retrieve sponsors for {0}.", account.Login);

        var result = new Dictionary<string, Sponsor>();

        foreach (var sponsor in raw)
        {
            if (!tiers.TryGetValue(sponsor.Tier.Id, out var tier))
                result.Add(sponsor.Login, sponsor with { Tier = AddTier(sponsor.Tier, tiers) });
            else
                result.Add(sponsor.Login, sponsor with { Tier = tier });
        }

        sponsors = result;
        return sponsors;
    }

    internal async Task<Dictionary<string, Tier>> GetTiersAsync()
    {
        if (tiers is not null)
            return tiers;

        var client = graphFactory.CreateClient("sponsorable");
        var result = new Dictionary<string, Tier>();

        var account = await GetSponsorable(cache, client, options);
        var json = await client.QueryAsync(GraphQueries.Tiers(account.Login));

        // TODO: should be an error?
        if (string.IsNullOrEmpty(json) ||
            JsonSerializer.Deserialize<RawTier[]>(json, JsonOptions.Default) is not { Length: > 0 } raw)
        {
            logger.LogWarning("No tiers were found for account {Login}.", account.Login);
            return result;
        }

        foreach (var item in raw)
        {
            var tier = new Tier(item.Id, item.Name, item.Description, item.Amount, item.OneTime, item.Previous);
            AddTier(tier, result);
        }

        tiers = result;
        return result;
    }

    /// <summary>
    /// Adds a tier, prior to populating its metadata from all ancestor tiers.
    /// </summary>
    static Tier AddTier(Tier tier, Dictionary<string, Tier> tiers)
    {
        var yaml = YamlRegex().Match(tier.Description)?.Groups["yaml"]?.Value;
        if (!string.IsNullOrEmpty(yaml) &&
            serializer.Deserialize<Dictionary<string, string>>(yaml) is { } meta)
        {
            foreach (var entry in meta)
            {
                tier.Meta.TryAdd(entry.Key, entry.Value);
            }
        }

        // Walk the tiers to aggregate metadata provided by lower tiers.
        // This avoids having to repeat metadata in each tier.
        var current = tier;
        while (true)
        {
            if (string.IsNullOrEmpty(current.Previous) ||
                !tiers.TryGetValue(current.Previous, out var previous))
                break;

            current = previous;
            // Don't overwrite existing metadata.
            foreach (var entry in current.Meta)
            {
                tier.Meta.TryAdd(entry.Key, entry.Value);
            }
        }

        tiers.Add(tier.Id, tier);
        return tier;
    }

    public async Task<string> GetRawManifestAsync()
    {
        if (!cache.TryGetValue<string>(JwtCacheKey, out var jwt) || string.IsNullOrEmpty(jwt))
        {
            var client = graphFactory.CreateClient("sponsorable");
            var account = await GetSponsorable(cache, client, options);

            var url = $"https://github.com/{account.Login}/.github/raw/{options.ManifestBranch ?? "main"}/sponsorlink.jwt";

            // Manifest should be public, so no need for any special HTTP client.
            using var http = httpFactory.CreateClient();
            var response = await http.GetAsync(url);

            logger.Assert(response.IsSuccessStatusCode,
                "Failed to retrieve manifest from {Url}: {StatusCode} {Reason}",
                url, (int)response.StatusCode, await response.Content.ReadAsStringAsync());

            jwt = await response.Content.ReadAsStringAsync();

            logger.Assert(SponsorableManifest.TryRead(jwt, out var manifest, out var missing),
                "Failed to read manifest due to missing required claim '{0}'", missing);

            // Manifest audience should match the sponsorable account to avoid weird issues?
            if (account.Login != manifest.Sponsorable)
                throw new InvalidOperationException($"Manifest sponsorable account {manifest.Sponsorable} does not match configured sponsorable account {account.Login}.");

            var cacheExpiration = TimeSpan.TryParse(options.ManifestExpiration, out var expiration) ? expiration : TimeSpan.FromHours(1);

            jwt = cache.Set(JwtCacheKey, jwt, cacheExpiration);
            manifest = cache.Set(ManifestCacheKey, manifest, cacheExpiration);

            Activity.Current?.AddEvent(new ActivityEvent("Sponsorable.ManifestRead",
                tags: new ActivityTagsCollection([KeyValuePair.Create<string, object?>("sponsorable", manifest.Sponsorable)])));
        }

        return jwt;
    }

    public async Task<SponsorableManifest> GetManifestAsync()
    {
        // Causes the manifest to be cached too.
        await GetRawManifestAsync();

        logger.Assert(cache.TryGetValue<SponsorableManifest>(ManifestCacheKey, out var manifest) && manifest is not null,
            "Failed to retrieve sponsorable manifest");

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

        var account = await sponsorable.QueryAsync(GraphQueries.Sponsorable(manifest.Sponsorable));
        logger.Assert(account is not null, "Failed to retrieve sponsorable account");

        if (logins.Contains(account.Login))
            type |= SponsorTypes.Team;

        // Use the sponsorable token since it has access to sponsorship info even if it's private
        var sponsoring = await sponsorable.QueryAsync(GraphQueries.IsSponsoredBy(account.Login, logins));
        logger.Assert(sponsoring is not null);

        // User is checked for auth on first line above
        var user = principal.FindFirst("urn:github:login")!.Value;

        if (sponsoring.Contains(user))
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
        if (options.NoContributors != true)
        {
            var contribs = await sponsor.QueryAsync(GraphQueries.ViewerContributedRepoOwners);
            if (contribs is not null &&
                contribs.Contains(manifest.Sponsorable))
            {
                type |= SponsorTypes.Contributor;
            }
        }

        // The OSS graph contains all contributors to active nuget packages that are open source.
        // See NuGetStatsCommand.cs
        if (options.NoOpenSource != true && await oss is { } graph && graph.Authors.ContainsKey(user))
            type |= SponsorTypes.OpenSource;

        // Determining if a user is an indirect sponsor via org emails is expensive, so if we already 
        // have a sponsor type, we return early.
        if (type != SponsorTypes.None)
            return type;

        // Add verified org email(s) > user's emails check (even if user's email is not public 
        // and the logged in account does not belong to the org). This covers the scenario where a 
        // user has multiple GH accounts, one for each org he works for (i.e. a consultant), and a 
        // personal account. The personal account would not be otherwise associated with any of his 
        // client's orgs, but he could still add his work emails to his personal account, keep them 
        // private and verified, and then use them to access and be considered an org sponsor.

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
        if (principal is not { Identity.IsAuthenticated: true })
            return null;

        var manifest = await GetManifestAsync();
        SponsorTypes sponsor;

        using (var activity = ActivityTracer.Source.StartActivity("Sponsor.Lookup"))
        {
            sponsor = await GetSponsorTypeAsync(principal);
            // coma-separated list of types can be parsed easily with parse_csv
            activity?.SetTag("Type", sponsor.ToString().Replace(" ", ""));
        }

        if (sponsor == SponsorTypes.None)
            return null;

        if (principal?.FindFirst("urn:github:login")?.Value is not string login)
            return null;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Iss, manifest.Issuer),
        };

        claims.AddRange(manifest.Audience.Select(x => new Claim(JwtRegisteredClaimNames.Aud, x)));
        claims.Add(new("client_id", manifest.ClientId));
        claims.Add(new(JwtRegisteredClaimNames.Sub, login));

        // check for each flags SponsorTypes and add claims accordingly
        // Note that in JWT IANA, roles is plural, unlike the more common role (singular) in claims-based auth.
        // See https://www.iana.org/assignments/jwt/jwt.xhtml
        if (sponsor.HasFlag(SponsorTypes.Team))
            claims.Add(new("roles", "team"));
        if (sponsor.HasFlag(SponsorTypes.Organization))
            claims.Add(new("roles", "org"));
        if (sponsor.HasFlag(SponsorTypes.User))
            claims.Add(new("roles", "user"));
        if (sponsor.HasFlag(SponsorTypes.Contributor))
            claims.Add(new("roles", "contrib"));
        if (sponsor.HasFlag(SponsorTypes.OpenSource))
            claims.Add(new("roles", "oss"));

        // Use shorthand JWT claim for emails. See https://www.iana.org/assignments/jwt/jwt.xhtml
        claims.AddRange(principal.Claims.Where(x => x.Type == ClaimTypes.Email).Select(x => new Claim(JwtRegisteredClaimNames.Email, x.Value)));

        return claims;
    }

    public async Task<Sponsor?> FindSponsorAsync(string? login)
    {
        if (login is null)
            return null;

        var graph = graphFactory.CreateClient("sponsorable");
        var account = await graph.QueryAsync(GraphQueries.FindAccount(login));
        var sponsorable = await GetSponsorable(cache, graph, options);
        var sponsors = await GetSponsorsAsync();

        if (sponsorable.Login == login)
        {
            return new Sponsor(login, account?.Type ?? AccountType.User, new Tier("team", "Team", "Team", 0, false)
            {
                Meta = { ["tier"] = "team" }
            })
            {
                Kind = SponsorTypes.Team,
            };
        }

        // This returns direct sponsors.
        if (sponsors.TryGetValue(login, out var sponsor))
            return sponsor;

        var orgs = await graph.QueryAsync(GraphQueries.UserOrganizations(login));
        if (orgs is not null && orgs.Any(x => x.Login == sponsorable.Login))
        {
            // This avoids having to check contributions for team members.
            return new Sponsor(login, account?.Type ?? AccountType.User, new Tier("team", "Team", "Team", 0, false)
            {
                Meta = { ["tier"] = "team" }
            })
            {
                Kind = SponsorTypes.Team,
            };
        }

        // Lookup for indirect sponsors, first via repo contributions. 
        if (options.NoContributors != true)
        {
            var contribs = await graph.QueryAsync(GraphQueries.UserContributions(login));
            if (contribs is not null && contribs.Contains(sponsorable.Login))
            {
                return new Sponsor(login, account?.Type ?? AccountType.User, new Tier("contrib", "Contributor", "Contributor", 0, false)
                {
                    Meta =
                    {
                        ["tier"] = "contrib",
                        ["label"] = "sponsor 💚",
                        ["color"] = "#BFFFD3"
                    }
                })
                {
                    Kind = SponsorTypes.Contributor,
                };
            }
        }

        if (options.NoOpenSource != true && await oss is { } data && data.Authors.ContainsKey(login))
        {
            return new Sponsor(login, account?.Type ?? AccountType.User, new Tier("oss", "Open Source", "Open Source", 0, false)
            {
                Meta =
                {
                    ["tier"] = "oss",
                    ["label"] = "sponsor 💚",
                    ["color"] = "#BFFFD3"
                }
            })
            {
                Kind = SponsorTypes.OpenSource,
            };
        }

        if (orgs is null || orgs.Length == 0)
            return null;

        Sponsor? orgSponsor = default;

        // Pick highest tier org.
        foreach (var org in orgs)
        {
            if (await FindSponsorAsync(org.Login) is { } found &&
                (orgSponsor is null || found.Tier.Amount > orgSponsor.Tier.Amount))
            {
                orgSponsor = found;
            }
        }

        return orgSponsor;
    }

    static async Task<Account> GetSponsorable(IMemoryCache cache, IGraphQueryClient client, SponsorLinkOptions options)
    {
        var key = nameof(SponsorsManager) + ".Sponsorable";
        if (cache.TryGetValue<Account>(key, out var value) && value != null)
            return value;

        var account = string.IsNullOrEmpty(options.Account) ?
            // default to the authenticated user login
            await client.QueryAsync(GraphQueries.ViewerAccount)
                ?? throw new ArgumentException("Failed to determine sponsorable user from configured GitHub token.") :
            await client.QueryAsync(GraphQueries.FindOrganization(options.Account))
                ?? await client.QueryAsync(GraphQueries.FindUser(options.Account))
                ?? throw new ArgumentException("Failed to determine sponsorable account from configured SponsorLink account.");

        cache.Set(key, account);

        return account;
    }

    record RawTier(string Id, string Name, string Description, int Amount, bool OneTime, string? Previous);

    [GeneratedRegex("<!--(?<yaml>.*)-->", RegexOptions.Singleline)]
    private static partial Regex YamlRegex();
}
