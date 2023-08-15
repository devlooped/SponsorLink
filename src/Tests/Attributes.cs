public class LocalFactAttribute : FactAttribute
{
    public LocalFactAttribute()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            Skip = "Non-CI test";
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