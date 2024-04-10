namespace Devlooped.Sponsors;

public record Account(string Login, AccountType Type);

public record Organization(string Login, string Email, string WebsiteUrl);

public enum AccountType
{
    User,
    Organization
}