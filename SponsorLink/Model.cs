using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Devlooped.SponsorLink;

public enum AppKind { Sponsor, Sponsorable}

public enum AppState { Installed, Suspended, Deleted }

/// <summary>
/// Accounts have basically two pieces of IDs, depending on which APIs 
/// you're using. The account ID is mostly used in REST APIs, and the NodeId 
/// on GraphQL. The login is sometimes the only allowed identifier in some 
/// REST APIs, but since it can be changed by the user, isn't as reliable 
/// as the other two.
/// </summary>
public record AccountId(string Id, string Login);

[PartitionKey(nameof(Authorization))]
public record Authorization([property: RowKey] string Account, string AccessToken, string Login);

public record Installation([RowKey] string Account, string Login, AppState State, string Secret);

[Table("Email")]
public record AccountEmail(string Account, string Login, string Email);

public record Sponsorship(
    string SponsorableId, string SponsorableLogin,
    string SponsorId, string SponsorLogin,
    int Amount)
{
    public DateOnly? ExpiresAt { get; init; }
    public bool Expired { get; init; } = false;
}

public record Webhook(string Id, string Payload);