﻿// <autogenerated />
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Devlooped;

partial class SponsorLink
{
    static string[] sponsorables = typeof(SponsorLink).Assembly
        .GetCustomAttributes(typeof(FundingAttribute), false)
        .OfType<FundingAttribute>()
        .Select(x => x.Account)
        .ToArray();

    static DateTime? installedOn;
    static HashSet<string> initialized = new();
    static ConcurrentDictionary<(string project, string sponsorable), bool?> sponsoring = new();

    /// <summary>
    /// Gets whether the user is sponsoring any of the accounts specified when <see cref="Initialize(string, string[])"/> 
    /// was invoked.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> if sponsoring status could not be determined (i.e. 
    /// <see cref="Status"/> is different than <see cref="ManifestStatus.Verified"/> 
    /// or git is not even installed).
    /// Otherwise, <see langword="true"/> if the git email from the git repository 
    /// root from the project directory specified when <see cref="Initialize(string, string[])"/> 
    /// was invoked is sponsoring any of the sponsorable accounts, or <see langword="false"/> otherwise.
    /// </returns>
    public static bool? IsSponsor { get; private set; } = null;

    /// <summary>
    /// Elapsed time since the consuming integration was installed.
    /// </summary>
    public static TimeSpan? Age => installedOn.HasValue ? DateTime.Now - installedOn.Value : default;

    /// <summary>
    /// Gets the current assembly <see cref="FundingAttribute"/> attributes as an array of 
    /// sponsorable accounts.
    /// </summary>
    public static string[] FundingAccounts => sponsorables;

    /// <summary>
    /// Initialize the sponsoring state for the given project and sponsorable accounts.
    /// </summary>
    /// <param name="project">The project that is initializing the state.</param>
    /// <param name="sponsorables">The accounts to check for active sponsorships.</param>
    /// <param name="installedOn">Optional date the integration project was installed on, for the purpose of checking 
    /// grace periods for sponsorships.</param>
    /// </param>
    public static void Initialize(string project, string[] sponsorables, DateTime? installedOn = default)
    {
        SponsorLink.installedOn = installedOn;
        if (initialized.Contains(project) || sponsorables.Length == 0) 
            return;

        // NOTE: this means we never do anything at all unless the manual process of 
        // refreshing the manifest is completed successfully. This is to avoid any
        // potential privacy issues with reading the email address from git config, 
        // and running external processes at all.
        if (Status != ManifestStatus.Verified)
            return;

        try
        {
            // First attempt to retrieve the git repo root, so we only probe once at that level
            var proc = Process.Start(new ProcessStartInfo("git", "rev-parse --show-toplevel")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = project
            });

            // If we failed to even start the process for whatever reason, assume we can't determine status.
            if (proc is null)
            {
                initialized.Add(project);
                IsSponsor = null;
                return;
            }

            proc.WaitForExit();

            // Couldn't run git config somehow
            if (proc.ExitCode != 0)
            {
                initialized.Add(project);
                IsSponsor = null;
                return;
            }

            var root = proc.StandardOutput.ReadToEnd().Trim();
            if (initialized.Contains(root))
                return;

            // Regardless of outcome, we only want to check once per root.
            initialized.Add(root);

            if (GetEmail(root) is string email)
            {
                // Manifest will be non-null since Status is Verified at this point.
                foreach (var sponsorable in sponsorables)
                {
                    if (Contains(email, sponsorable) == true)
                    {
                        IsSponsor = true;
                        return;
                    }
                }

                IsSponsor = false;
            }
            else
            {
                // NOTE: this means that changing the email requires a process restart, 
                // just as if the manifest is expired or invalid.
                IsSponsor = null;
            }
        }
        catch (Exception e)
        {
            // We don't want to fail if anything goes wrong. Trace conditionally 
            // for troubleshooting purposes.
            Tracing.Trace(e.Message);
        }
        finally
        {
            // We always add the project regardless of check outcome.
            initialized.Add(project);
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
