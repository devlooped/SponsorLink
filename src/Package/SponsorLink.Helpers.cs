using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace Devlooped;

partial class SponsorLink
{
    static string[] sponsorables = typeof(SponsorLink).Assembly
        .GetCustomAttributes(typeof(FundingAttribute), false)
        .OfType<FundingAttribute>()
        .Select(x => x.Account)
        .ToArray();

    static ConcurrentDictionary<(string repository, string sponsorable), bool?> sponsoring = new();

    /// <summary>
    /// Whether the current process is running in an IDE, either 
    /// <see cref="IsVisualStudio"/> or <see cref="IsRider"/>.
    /// </summary>
    public static bool IsEditor => IsVisualStudio || IsRider;

    /// <summary>
    /// Whether the current process is running as part of an active Visual Studio instance.
    /// </summary>
    public static bool IsVisualStudio =>
        Environment.GetEnvironmentVariable("ServiceHubLogSessionKey") != null ||
        Environment.GetEnvironmentVariable("VSAPPIDNAME") != null;

    /// <summary>
    /// Whether the current process is running as part of an active Rider instance.
    /// </summary>
    public static bool IsRider =>
        Environment.GetEnvironmentVariable("RESHARPER_FUS_SESSION") != null ||
        Environment.GetEnvironmentVariable("IDEA_INITIAL_DIRECTORY") != null;


    /// <summary>
    /// Checks whether the user is sponsoring any of the accounts the current 
    /// assembly is annotated with via the <see cref="FundingAttribute"/> attribute.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> if sponsoring status cannot be determined (i.e. 
    /// <see cref="Status"/> is different than <see cref="ManifestStatus.Verified"/>). 
    /// Otherwise, <see langword="true"/> if the git email from the git repository 
    /// root from the <paramref name="workingDir"/> is sponsoring any of the sponsorable 
    /// accounts, or <see langword="false"/> otherwise.
    /// </returns>
    public static bool? IsSponsoring(string workingDir)
    {
        if (Status != ManifestStatus.Verified)
            return default;

        if (sponsorables.Length == 0)
            throw new InvalidOperationException("Current assembly does not have any [assembly: Funding] attributes.");

        bool? result = null;

        foreach (var sponsorable in sponsorables)
        {
            if (IsSponsoring(workingDir, sponsorable) is bool sponsoring)
            {
                // Capture a potential 'false' result, but keep looking for a 'true' one.
                result = sponsoring;
                if (sponsoring)
                    return sponsoring;
            }
        }

        return result;
    }

    /// <summary>
    /// Checks whether the user is sponsoring the given sponsorable account.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> if sponsoring status cannot be determined (i.e. 
    /// <see cref="Status"/> is different than <see cref="ManifestStatus.Verified"/>). 
    /// Otherwise, <see langword="true"/> if the git email from the git repository 
    /// root from the <paramref name="workingDir"/> is sponsoring the sponsorable 
    /// account, or <see langword="false"/> otherwise.
    /// </returns>
    public static bool? IsSponsoring(string workingDir, string sponsorable)
    {
        // NOTE: this means we never do anything at all unless the manual process of 
        // refreshing the manifest is completed successfully. This is to avoid any
        // potential privacy issues with reading the email address from git config, 
        // and running external processes at all.
        if (Status != ManifestStatus.Verified)
            return default;

        if (sponsoring.TryGetValue((workingDir, sponsorable), out var sponsored))
            return sponsored;

        try
        {
            var proc = Process.Start(new ProcessStartInfo("git", "rev-parse --show-toplevel")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            });

            if (proc is null)
            {
                sponsoring.TryAdd((workingDir, sponsorable), null);
                return default;
            }

            proc.WaitForExit();

            // Couldn't run git config somehow
            if (proc.ExitCode != 0)
                return default;

            var root = proc.StandardOutput.ReadToEnd().Trim();
            if (sponsoring.TryGetValue((root, sponsorable), out sponsored))
            {
                // We were missing a key for the given working dir
                sponsoring.TryAdd((workingDir, sponsorable), sponsored);
                return sponsored;
            }

            // Else, we need to lookup the email and manifest. 
            // NOTE: we check only once regardless.
            if (GetEmail(root) is string email)
            {
                // Manifest will be non-null since Status is Verified (first line).
                sponsored = Contains(email, sponsorable);
                sponsoring.TryAdd((root, sponsorable), sponsored);
                sponsoring.TryAdd((workingDir, sponsorable), sponsored);
                return sponsored;
            }
            else
            {
                // NOTE: this means that changing the email requires a process restart, 
                // as as if the manifest is expired or invalid.
                sponsoring.TryAdd((root, sponsorable), null);
                sponsoring.TryAdd((workingDir, sponsorable), null);
                return default;
            }
        }
        catch
        {
            // Git not even installed.
            sponsoring.TryAdd((workingDir, sponsorable), null);
            return default;
        }
    }

    /// <summary>
    /// Attempts to get the email address of the current user, if available 
    /// for the purpose of checking whether they are a sponsor.
    /// </summary>
    /// <remarks>
    /// A package author can rely on other mechanisms for detemining the 
    /// user's email to check against the sponsor list, this is just a helper 
    /// for *one* such way. Alternatives include reading from an environment 
    /// variable, a file, etc.
    /// </remarks>
    public static string? GetEmail(string workingDirectory)
    {
        // We never attempt to read anything unless users got a manifest 
        // which requires them to consent to email reading.
        if (Status == ManifestStatus.NotFound)
            return default;

        try
        {
            var proc = Process.Start(new ProcessStartInfo("git", "config --get user.email")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            });

            if (proc is null)
                return default;

            proc.WaitForExit();

            // Couldn't run git config somehow
            if (proc.ExitCode != 0)
                return default;

            return proc.StandardOutput.ReadToEnd().Trim();
        }
        catch
        {
            // Git not even installed.
            return default;
        }
    }
}
