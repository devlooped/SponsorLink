using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Devlooped;

public class AuthOptions
{
    public required string ConsumerKey { get; set; }
    public required string ConsumerSecret { get; set; }
    public required string AccessToken { get; set; }
    public required string AccessTokenSecret { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(ConsumerKey) &&
        !string.IsNullOrEmpty(ConsumerSecret) &&
        !string.IsNullOrEmpty(AccessToken) &&
        !string.IsNullOrEmpty(AccessTokenSecret);
}

public class AuthOptionsValidation(IConfiguration configuration) : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        // Brings in the configuration from the X_ "section" from envvars by prefix
        configuration.Bind(options);
        return Validate(options);
    }

    public static ValidateOptionsResult Validate(AuthOptions options)
    {
        var failures = new List<string>();

        if (options.ConsumerKey == null)
            failures.Add("Missing X:ConsumerKey configuration");
        if (options.ConsumerSecret == null)
            failures.Add("Missing X:ConsumerSecret configuration");
        if (options.AccessToken == null)
            failures.Add("Missing X:AccessToken configuration");
        if (options.AccessTokenSecret == null)
            failures.Add("Missing X:AccessTokenSecret configuration");

        if (failures.Count > 0)
        {
            failures.Insert(0, "You are not logged in");
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}