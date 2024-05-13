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

public record Tier(string Name, string Description, int Amount, bool OneTime)
{
    public Dictionary<string, string> Meta { get; init; } = [];
}

public record OwnerRepo(string Owner, string Repo);

public record FundedRepository(string OwnerRepo, string[] Sponsorables);