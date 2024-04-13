namespace Devlooped.Sponsors;

public record Account(string Login, AccountType Type);

public enum AccountType
{
    User,
    Organization
}

public record Organization(string Login, string Email, string WebsiteUrl);

public record Sponsorship(string Tier, double Amount);