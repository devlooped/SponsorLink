using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Devlooped.SponsorLink;

public enum AppKind { Sponsor, Sponsorable}

public enum AppState { Installed, Suspended, Deleted }

/// <summary>
/// Accounts have basically three pieces of IDs, depending on which APIs 
/// you're using. The account ID is mostly used in REST APIs, and the NodeId 
/// on GraphQL. The login is sometimes the only allowed identifier in some 
/// REST APIs, but since it can be changed by the user, isn't as reliable 
/// as the other two.
/// </summary>
public record AccountId(long Id, string NodeId, string Login)
{
    public static implicit operator string(AccountId account) => $"{account.Login}.{account.Id}";
}

public record Account([property: Browsable(false)] AccountId Id, AppState State, bool Authorized)
{
    public string? AccessToken { get; init; }
    [JsonIgnore]
    public bool IsActive => State == AppState.Installed && Authorized;

    [JsonIgnore]
    [RowKey]
    public string AccountId => Id.Id.ToString();

    [JsonIgnore]
    public string Login => Id.Login;

    [JsonIgnore]
    public string NodeId => Id.NodeId;
}

[Table("Email")]
public record AccountEmail([property: Browsable(false)] AccountId Id, string Email)
{
    [JsonIgnore]
    public string AccountId => Id.Id.ToString();

    [JsonIgnore]
    public string Login => Id.Login;

    [JsonIgnore]
    public string NodeId => Id.NodeId;
}