namespace App;

public record User(string Login, string AccessToken);

public record UserEmail(string Email, string Login);
