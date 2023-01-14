namespace Devlooped.SponsorLink;

public record User(int Id, string Login, string Email, string AccessToken);

public record Sponsorship(
    int SponsorableId,
    int SponsorId, 
    string SponsorableLogin, 
    string SponsorLogin,
    int Amount, 
    DateOnly CreatedAt, 
    DateOnly? ExpiresAt);

public record EmailUser(string Email, int Id);
