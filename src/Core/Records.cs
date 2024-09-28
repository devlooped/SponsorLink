using System.Collections.Concurrent;
using System.ComponentModel;

namespace Devlooped.Sponsors;

public record Account(string Login, AccountType Type);

public enum AccountType
{
    User,
    Organization
}

public record Organization(string Login, string Email, string WebsiteUrl);

public record Sponsorship(string Sponsorable, [property: Browsable(false)] string Tier, int Amount,
#if NET6_0_OR_GREATER
    DateOnly CreatedAt,
#else
    DateTime CreatedAt,
#endif
    [property: DisplayName("One-time")] bool OneTime);

public record Sponsor(string Login, AccountType Type, Tier Tier)
{
    public SponsorTypes Kind { get; init; } = Type == AccountType.Organization ? SponsorTypes.Organization : SponsorTypes.User;
}

public record Tier(string Id, string Name, string Description, int Amount, bool OneTime, string? Previous = null)
{
    public Dictionary<string, string> Meta { get; init; } = [];
}

public record OwnerRepo(string Owner, string Repo);

public record FundedRepository(string OwnerRepo, string[] Sponsorables);

public record OpenSource(ConcurrentDictionary<string, HashSet<string>> Authors, ConcurrentDictionary<string, HashSet<string>> Repositories, ConcurrentDictionary<string, ConcurrentDictionary<string, long>> Packages)
{
    public OpenSourceSummary Totals => new(this);

    public class OpenSourceSummary(OpenSource source)
    {
        public long Authors => source.Authors.Count;
        public long Repositories => source.Repositories.Count;
        public long Packages => source.Packages.Sum(x => x.Value.Count);
        public long Downloads => source.Packages.Sum(x => x.Value.Sum(y => y.Value));
    }
}
