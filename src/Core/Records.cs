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
