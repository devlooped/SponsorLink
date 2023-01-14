using Microsoft.Extensions.DependencyInjection;

namespace Devlooped.SponsorLink;

[Service]
public class SponsorsManager
{
    public SponsorsManager(IHttpClientFactory httpFactory, EventStream events)
    {

    }

    public async Task InstallAsync(AppKind kind, string accountId, string? note = default)
    {
        
    }

    public async Task UninstallAsync(AppKind kind, string accountId, string? note = default)
    {

    }

    public async Task SponsorAsync(string sponsorable, string sponsor, int amount, DateOnly? expiresAt = null, string? note = default)
    {
        
    }

    public async Task SponsorUpdateAsync(string sponsorable, string sponsor, int amount, string? note = default)
    {
    }

    public async Task UnsponsorAsync(string sponsorable, string sponsor, DateOnly cancelAt, string? note = default)
    {
        
    }

    public async Task CheckExpirationsAsync()
    {
    }
}
