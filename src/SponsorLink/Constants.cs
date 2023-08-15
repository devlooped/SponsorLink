namespace Devlooped.SponsorLink;

public static class Constants
{
    public static AccountId MoqAccount { get; } = new AccountId("MDEyOk9yZ2FuaXphdGlvbjE0MzQ5MzQ=", "moq");
    public static AccountId DevloopedAccount { get; } = new AccountId(DevloopedId, DevloopedLogin);
    /// <summary>
    /// The Node ID of the Devlooped account.
    /// </summary>
    public const string DevloopedId = "MDEyOk9yZ2FuaXphdGlvbjYxNTMzODE4";
    /// <summary>
    /// devlooped
    /// </summary>
    public const string DevloopedLogin = "devlooped";
    /// <summary>
    /// The log analytics workspace to use for querying.
    /// </summary>
    public const string LogAnalyticsWorkspaceId = "7f38af71-fab9-4d5c-9a32-743ed17c2d1a";
}
