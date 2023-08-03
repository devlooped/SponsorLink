using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Devlooped;

/// <summary>
/// The sponsoring status.
/// </summary>
public enum SponsorStatus
{
    /// <summary>
    /// The SponsorLink GitHub is not installed on the user's personal account.
    /// </summary>
    AppMissing,
    /// <summary>
    /// The user is not sponsoring the given sponsor account.
    /// </summary>
    NotSponsoring,
    /// <summary>
    /// The user has the SponsorLink GitHub app installed and is sponsoring the given sponsor account.
    /// </summary>
    Sponsoring,
    /// <summary>
    /// The user is a member of the given sponsorable account and does not need to have a sponsorship.
    /// </summary>
    Member,
}

/// <summary>
/// Allows directly checking the sponsor status from any consuming code, 
/// such as command line app, build task or analyzer.
/// </summary>
public static class SponsorCheck
{
    static string ContextQuery { get; }

    static SponsorCheck()
    {
        var sb = new StringBuilder()
            .Append($"&sl={ThisAssembly.Info.InformationalVersion}");

        if (Environment.GetEnvironmentVariable("CODESPACES") == "true")
            sb.Append("&codespace=true");

        if (SessionManager.IsVisualStudio)
        {
            sb.Append("&editor=vs")
              .Append("&editor.sku=")
              .Append(Environment.GetEnvironmentVariable("VSSKUEDITION")?.ToLowerInvariant());

            if (Environment.GetEnvironmentVariable("VSAPPIDDIR") is string appdir && 
                Directory.Exists(appdir) && 
                File.Exists(Path.Combine(appdir, "devenv.isolation.ini")))
            {
                var value = File.ReadAllLines(Path.Combine(appdir, "devenv.isolation.ini"))
                    .Where(line => line.StartsWith("InstallationVersion="))
                    .Select(line => line.Substring("InstallationVersion=".Length))
                    .FirstOrDefault()?.Trim();
                if (value != null && Version.TryParse(value, out var version))
                {
                    sb.Append("&editor.version=").Append(version.ToString(2));
                }
            }
        }
        else if (SessionManager.IsRider)
        {
            sb.Append("&editor=rider");
            if (Environment.GetEnvironmentVariable("IDEA_INITIAL_DIRECTORY") is string ideadir && 
                Regex.Match(ideadir, "\\d\\d\\d\\d\\.\\d\\d?") is Match match && 
                match.Success)
            {
                sb.Append("&editor.version=").Append(match.Value);
            }
        }

        ContextQuery = sb.ToString();
    }

    /// <summary>
    /// Checks the sponsoring status with the given parameters.
    /// </summary>
    /// <param name="workingDirectory">
    /// The working directory to use for the check. Used to 
    /// determine the git email to use, since this can be overriden 
    /// per repository.</param>
    /// <param name="sponsorable">The sponsor account that should be sponsored by the user.</param>
    /// <param name="product">The name of the product being checked.</param>
    /// <param name="packageId">Optional package identifier used for telemetry. Defaults to <paramref name="product"/>.</param>
    /// <param name="version">Optional package version used for telemetry. Defaults to empty.</param>
    /// <param name="http">Optional <see cref="HttpClient"/> with custom settings (i.e. proxy, timeout).</param>
    /// <returns>The <see cref="SponsorStatus"/> or <see langword="null"/> if status cannot be 
    /// checked (for example if the network is unavailable, the <paramref name="workingDirectory"/> doesn't 
    /// exist or a configured git email cannot be determined for the check.</returns>
    public static async Task<SponsorStatus?> CheckAsync(
        string workingDirectory, string sponsorable, string product,
        string? packageId = default, string? version = default,
        HttpClient? http = default)
    {
        // If there is no network at all, don't do anything.
        if (!NetworkInterface.GetIsNetworkAvailable() ||
            !Directory.Exists(workingDirectory))
            return default;

        var email = GetEmail(workingDirectory);
        // No email configured in git, or there is no git at all.
        if (string.IsNullOrEmpty(email))
            return default;

        var data = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(email));
        var hash = Base62.Encode(BigInteger.Abs(new BigInteger(data)));
        var query = $"account={sponsorable}&product={product}&package={packageId}&version={version}" + 
            $"&noreply=" + email!.EndsWith("@users.noreply.github.com").ToString().ToLowerInvariant() + 
            ContextQuery;

        // Check app install and sponsoring status
        var installed = await CheckUrlAsync(http ?? HttpClientFactory.Default, $"https://cdn.devlooped.com/sponsorlink/apps/{hash}?{query}", default);
        // Timeout, network error, proxy config issue, etc., exit quickly
        if (installed == null)
            return default;

        if (installed == false)
            return SponsorStatus.AppMissing;

        var sponsoring = await CheckSponsorAsync(http ?? HttpClientFactory.Default, $"https://cdn.devlooped.com/sponsorlink/{sponsorable}/{hash}?{query}", default);
        if (sponsoring == null)
            return default;

        return sponsoring;
    }

    static internal void ReportBroken(
        string reason, string? workingDirectory,
        SponsorLinkSettings settings, HttpClient? http = default)
    {
#if DEBUG
        Debugger.Launch();
#endif

        // If there is no network at all, don't do anything.
        if (!NetworkInterface.GetIsNetworkAvailable())
            return;

        var email = GetEmail(workingDirectory ?? Directory.GetCurrentDirectory());
        // No email configured in git, or there is no git at all.
        if (string.IsNullOrEmpty(email))
            return;

        var data = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(email));
        var hash = Base62.Encode(BigInteger.Abs(new BigInteger(data)));
        var query = $"reason={reason}&account={settings.Sponsorable}&product={settings.Product}&package={settings.PackageId}&version={settings.Version}" +
            "&noreply=" + email!.EndsWith("@users.noreply.github.com").ToString().ToLowerInvariant() + 
            ContextQuery;

        CheckUrlAsync(http ?? HttpClientFactory.Default,
            $"https://cdn.devlooped.com/sponsorlink/broken/{settings.Sponsorable}/{hash}?{query}", default)
            .FireAndForget();
    }

    static string? GetEmail(string workingDirectory)
    {
        try
        {
            var proc = Process.Start(new ProcessStartInfo("git", "config --get user.email")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            });
            proc.WaitForExit();

            // Couldn't run git config, so we can't check for sponsorship, no email to check.
            if (proc.ExitCode != 0)
                return null;

            return proc.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            // Git not even installed.
        }

        return null;
    }

    static async Task<bool?> CheckUrlAsync(HttpClient http, string url, CancellationToken cancellation)
    {
        try
        {
            // We perform a GET since that can be cached by the CDN, but HEAD cannot.
            var response = await http.GetAsync(url, cancellation);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) 
        {
            if (!cancellation.IsCancellationRequested)
                Tracing.Trace($"{nameof(CheckUrlAsync)}({url}): \r\n{ex}");

            return null;
        }
    }

    /// <summary>
    /// The three possible states when checking a blob URL are: 
    /// 1. null, if the check could not be performed (network error, timeout, etc.) or 404
    /// 2. true: the blob exists and its content is empty
    /// 3. [true|false]: the blob exists, its content is not empty and its single byte payload is parsed as a boolean.
    /// </summary>
    static async Task<SponsorStatus?> CheckSponsorAsync(HttpClient http, string url, CancellationToken cancellation)
    {
        try
        {
            // We perform a GET since that can be cached by the CDN, but HEAD cannot.
            var response = await http.GetAsync(url, cancellation);
            if (!response.IsSuccessStatusCode)
                return SponsorStatus.NotSponsoring;

            var content = await response.Content.ReadAsByteArrayAsync();
            if (content == null || content.Length == 0)
                return SponsorStatus.Sponsoring;

            return content[0] == 1 ? SponsorStatus.Member : SponsorStatus.Sponsoring;
        }
        catch (Exception ex)
        {
            if (!cancellation.IsCancellationRequested)
                Tracing.Trace($"{nameof(CheckUrlAsync)}({url}): \r\n{ex}");

            return null;
        }
    }
}
