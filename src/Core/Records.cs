using System.Collections.Concurrent;
using System.ComponentModel;
using System.Security.Cryptography;
using Humanizer;

namespace Devlooped.Sponsors;

public record Account(string Login, AccountType Type);

public enum AccountType
{
    User,
    Organization
}

public record Organization(string Login, string Email, string WebsiteUrl);

public record Sponsorship(string Sponsorable, [property: Browsable(false)] string Tier, int Amount,
#if NET6_0_OR_GREATER
    DateOnly CreatedAt,
#else
    DateTime CreatedAt,
#endif
    [property: DisplayName("One-time")] bool OneTime);

public record Sponsor(string Login, AccountType Type, Tier Tier)
{
    public SponsorTypes Kind { get; init; } = Type == AccountType.Organization ? SponsorTypes.Organization : SponsorTypes.User;
}

public record Tier(string Id, string Name, string Description, int Amount, bool OneTime, string? Previous = null)
{
    public Dictionary<string, string> Meta { get; init; } = [];
}

public record OwnerRepo(string Owner, string Repo);

public record FundedRepository(string OwnerRepo, string[] Sponsorables);

public record OpenSource(ConcurrentDictionary<string, HashSet<string>> Authors, ConcurrentDictionary<string, HashSet<string>> Repositories, ConcurrentDictionary<string, ConcurrentDictionary<string, long>> Packages)
{
    OpenSourceSummary? summary;
    OpenSourceTotals? totals;

    public OpenSource() : this(new(FnvHashComparer.Default), new(FnvHashComparer.Default), new(FnvHashComparer.Default))
    {
    }

    public OpenSourceSummary Summary => summary ??= new(Totals);

    public OpenSourceTotals Totals => totals ??= new(this);

    public class OpenSourceTotals(OpenSource source)
    {
        public double Authors => source.Authors.Count;
        public double Repositories => source.Repositories.Count;
        public double Packages => source.Packages.Sum(x => x.Value.Count);
        public double Downloads => source.Packages.Sum(x => x.Value.Sum(y => y.Value));
    }

    public class OpenSourceSummary(OpenSourceTotals totals)
    {
        public string Authors => totals.Authors.ToMetric(decimals: 1);
        public string Repositories => totals.Repositories.ToMetric(decimals: 1);
        public string Packages => totals.Packages.ToMetric(decimals: 1);
        public string Downloads => totals.Downloads.ToMetric(decimals: 1);
    }
}

public record Rate(RateLimit General, RateLimit GraphQL);

public record RateLimit(int Limit, int Used, int Remaining, long Reset);