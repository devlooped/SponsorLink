using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devlooped.Sponsors;

public class SponsorsManager(
    IOptions<SponsorLinkOptions> options, IHttpClientFactory httpFactory,
    IGraphQueryClientFactory graphFactory,
    IMemoryCache cache, ILogger<SponsorsManager> logger)
{
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
            manifest = SponsorableManifest.FromJwt(jwt);

            var audience = manifest.Audience;

            // Set the account type
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
    /// Gets the list of accounts related to the authenticated user, 
    /// that are sponsoring the sponsorable account. These can be the 
    /// current user or any organization he belongs to.
    /// </summary>
    public async Task<SponsorType> GetSponsorTypeAsync(ClaimsPrincipal? principal = default)
    {
        principal ??= ClaimsPrincipal.Current;

        if (principal is not { Identity.IsAuthenticated: true })
            return SponsorType.None;

        var sponsor = graphFactory.CreateClient("sponsor");
        var sponsorable = graphFactory.CreateClient("sponsorable");

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
            return SponsorType.Member;

        // Use the sponsorable token since it has access to sponsorship info even if it's private
        var sponsoring = await sponsorable.QueryAsync(GraphQueries.IsSponsoredBy(audience, logins));
        logger.Assert(sponsoring is not null);

        // User is checked for auth on first line above
        if (principal.FindFirst("urn:github:login") is { Value.Length: > 0 } claim &&
            sponsoring.Contains(claim.Value))
        {
            // the user is directly sponsoring
            return SponsorType.User;
        }

        // Next we check for direct contributions, which is more "valuable" than indirect org sponsorship
        var contribs = await sponsor.QueryAsync(GraphQueries.ViewerContributions);
        if (contribs is not null &&
            contribs.Contains(manifest.Audience))
        {
            return SponsorType.Contributor;
        }

        if (sponsoring.Length > 0)
            return SponsorType.Organization;

        // TODO: add verified org email(s) > user's emails check (even if user's email is not public 
        // and the logged in account does not belong to the org). This covers the scenario where a 
        // user has multiple GH accounts, one for each org he works for (i.e. a consultant), and a 
        // personal account. The personal account would not be otherwise associated with any of his 
        // client's orgs, but he could still add his work emails to his personal account, keep them 
        // private and verified, and then use them to access and be considered an org sponsor.

        // TODO: cache this?
        var sponsoringOrgs = await sponsorable.QueryAsync(GraphQueries.VerifiedSponsoringOrganizations(account.Login));
        if (sponsoringOrgs is null || sponsoringOrgs.Length == 0)
            return SponsorType.None;

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
                    return SponsorType.Organization;
            }
        }

        return SponsorType.None;
    }

    public async Task<List<Claim>?> GetSponsorClaimsAsync(ClaimsPrincipal? principal = default)
    {
        principal ??= ClaimsPrincipal.Current;
        var manifest = await GetManifestAsync();

        var sponsor = await GetSponsorTypeAsync(principal);
        if (sponsor == SponsorType.None ||
            principal?.FindFirst("urn:github:login")?.Value is not string id)
            return null;

        // TODO: add more claims in the future? tier, others?
        var claims = new List<Claim>
        {
            new("iss", manifest.Issuer),
            new("aud", manifest.Audience),
            new("client_id", manifest.ClientId),
            new("sub", id),
            new("sponsor", sponsor.ToString().ToLowerInvariant()),
        };

        // Use shorthand JWT claim for emails. See https://www.iana.org/assignments/jwt/jwt.xhtml
        claims.AddRange(principal.Claims.Where(x => x.Type == ClaimTypes.Email).Select(x => new Claim("email", x.Value)));

        return claims;
    }
}
