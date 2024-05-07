using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

/// <summary>
/// Status of a manifest refresh operation.
/// </summary>
public enum ManifestStatus
{
    /// <summary>
    /// SponsorLink manifest is invalid.
    /// </summary>
    InvalidManifest,
    /// <summary>
    /// The manifest has an audience that doesn't match the sponsorable account.
    /// </summary>
    AccountMismatch,
    /// <summary>
    /// Could not retrieve a credentials token to sync the manifest.
    /// </summary>
    MissingCredentials,
    /// <summary>
    /// SponsorLink manifest not found for the given account, so it's not supported.
    /// </summary>
    NotSupported,
    /// <summary>
    /// The user is not sponsoring (directly or indirectly via org or contributions)
    /// </summary>
    NotSponsoring,
    /// <summary>
    /// Unexpected error during synchronization with the manifest issuer backend.
    /// </summary>
    SyncFailure,
    /// <summary>
    /// Synchronization was successful.
    /// </summary>
    Success,
}

/// <summary>
/// Allows synchronizing client sponsor manifests.
/// </summary>
public class SponsorManifest
{
    /// <summary>
    /// Refreshes the sponsor manifest for the given sponsorable account.
    /// </summary>
    /// <param name="sponsorable">Sponsorable account to sync manifest from.</param>
    /// <param name="http">The <see cref="HttpClient"/> to use for API requests.</param>
    /// <param name="progress">Progress reporting.</param>
    /// <param name="authenticate">Authentication delegate to retrieve a user auth token given the GitHub app's client id.</param>
    /// <param name="issuerOverride">For debugging purposes, allows overriding the issuer in the manifest with a local endpoint.</param>
    /// <returns>The status of the manifest synchronization.</returns>
    public static async Task<ManifestStatus> RefreshAsync(string sponsorable, HttpClient http,
        IProgress<string> progress, Func<string, Task<string?>> authenticate,
        string? issuerOverride = default)
    {
        // Try to detect sponsorlink manifest in the sponsorable .github repo
        var url = $"https://github.com/{sponsorable}/.github/raw/main/sponsorlink.jwt";
        progress.Report($"fetching SponsorLink manifest from {url}");

        // Manifest should be public, so no need for any special HTTP client.
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            progress.Report("SponsorLink manifest not found");
            return ManifestStatus.NotSupported;
        }

        var jwt = await response.Content.ReadAsStringAsync();
        if (!SponsorableManifest.TryRead(jwt, out var manifest, out var missingClaim))
        {
            progress.Report($"SponsorLink manifest is missing required claim '{missingClaim}'.");
            return ManifestStatus.InvalidManifest;
        }

        var audience = manifest.Audience;
        if (Uri.TryCreate(manifest.Audience, UriKind.Absolute, out var audienceUri))
            audience = audienceUri.Segments[^1].TrimEnd('/');

        // Manifest audience should match the sponsorable account to avoid weird issues?
        if (sponsorable != audience)
        {
            progress.Report($"Manifest audience {audience} does not match account {sponsorable}.");
            return ManifestStatus.AccountMismatch;
        }

        progress.Report($"SponsorLink manifest detected");
        if (await authenticate(manifest.ClientId) is not string token)
        {
            progress.Report($"Could not authenticate with {sponsorable} GitHub app");
            return ManifestStatus.MissingCredentials;
        }

        var issuer = issuerOverride ?? manifest.Issuer;

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(issuer), "sync"));
        request.Headers.Authorization = new("Bearer", token);
        request.Headers.Accept.Add(new("application/jwt"));
        response = await http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // User is not a sponsor
            progress.Report("you are not a sponsor");
            return ManifestStatus.NotSponsoring;
        }

        var reportIssue = "Please report to https://github.com/{sponsorable}/.github/issues.";

        if (!response.IsSuccessStatusCode)
        {
            progress.Report("Unexpected failure on sync. " + reportIssue + " (status code {response.StatusCode})");
            return ManifestStatus.SyncFailure;
        }

        jwt = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(jwt))
        {
            progress.Report("Empty JWT received. " + reportIssue);
            return ManifestStatus.SyncFailure;
        }

        if (new JwtSecurityTokenHandler().CanReadToken(jwt) == false)
        {
            progress.Report("Invalid JWT received. " + reportIssue);
            return ManifestStatus.SyncFailure;
        }

        try
        {
            // verify no tampering with the manifest
            var claims = manifest.Validate(jwt, out var sectoken);

            progress.Report($"Found sponsor roles: {string.Join(',', claims.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => $"[lime]{c.Value}[/]"))}");

            // save the manifest for later use. note that for now, we only support GitHub sponsors manifests.
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SponsorLink", "GitHub");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, sponsorable + ".jwt"), jwt);

            return ManifestStatus.Success;
        }
        catch (SecurityTokenException e)
        {
            progress.Report($"Invalid JWT received: {e.Message}. " + reportIssue);
            return ManifestStatus.SyncFailure;
        }
    }
}
