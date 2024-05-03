using Microsoft.Extensions.Configuration;

public class SecretsFactAttribute : FactAttribute
{
    public SecretsFactAttribute(params string[] secrets)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<SecretsFactAttribute>()
            .Build();

        var missing = new HashSet<string>();

        foreach (var secret in secrets)
        {
            if (string.IsNullOrEmpty(configuration[secret]))
                missing.Add(secret);
        }

        if (missing.Count > 0)
            Skip = "Missing user secrets: " + string.Join(',', missing);
    }
}

public class LocalFactAttribute : FactAttribute
{
    public LocalFactAttribute(params string[] secrets)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            Skip = "Non-CI test";

        if (secrets.Length > 0)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<SecretsFactAttribute>()
                .Build();

            var missing = new HashSet<string>();

            foreach (var secret in secrets)
            {
                if (string.IsNullOrEmpty(configuration[secret]))
                    missing.Add(secret);
            }

            if (missing.Count > 0)
                Skip = "Missing user secrets: " + string.Join(',', missing);
        }
    }
}

public class CIFactAttribute : FactAttribute
{
    public CIFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            Skip = "CI-only test";
    }
}

public class LocalTheoryAttribute : TheoryAttribute
{
    public LocalTheoryAttribute()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            Skip = "Non-CI test";
    }
}

public class CITheoryAttribute : TheoryAttribute
{
    public CITheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            Skip = "CI-only test";
    }
}