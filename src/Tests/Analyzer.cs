using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Devlooped.Tests;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestSponsorLink : DiagnosticAnalyzer
{
    static DiagnosticDescriptor descriptor = new("IDE001", "IDE", "IDE", "Design", DiagnosticSeverity.Warning, true, customTags: ["CompilationEnd"]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        // Only warns in editor builds.
        if (IsEditor)
            context.RegisterCompilationAction(c => c.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None)));
    }

    public static bool IsEditor => IsVisualStudio || IsRider;

    public static bool IsVisualStudio =>
        Environment.GetEnvironmentVariable("ServiceHubLogSessionKey") != null ||
        Environment.GetEnvironmentVariable("VSAPPIDNAME") != null;

    public static bool IsRider =>
        Environment.GetEnvironmentVariable("RESHARPER_FUS_SESSION") != null ||
        Environment.GetEnvironmentVariable("IDEA_INITIAL_DIRECTORY") != null;
}

public class AnalyzerTests(ITestOutputHelper Output)
{
    [Fact]
    public async Task CreateSponsorLinkAsync()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<TestSponsorLink, DefaultVerifier>();

        test.TestCode = "// ";

        if (TestSponsorLink.IsEditor)
        {
            Output.WriteLine("Running inside editor");
            test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerWarning("IDE001"));
        }
        else
        {
            Output.WriteLine("Running outside editor");
        }

        await test.RunAsync();
    }

    // Showcases how to read the user's email using LibGit2Sharp
    [LocalFact]
    public void LibGitConfig()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        var root = default(string);
        while (dir != null)
        {
            if (Path.Combine(dir.FullName, ".git") is string path &&
                Directory.Exists(path))
            {
                root = path;
                break;
            }
            dir = dir.Parent;
        }

        Skip.If(root == null, "No git repository found.");

        var config = LibGit2Sharp.Configuration.BuildFrom(root);
        var email = config.Get<string>("user.email").Value;

        Assert.Contains("@", email);
        Output.WriteLine(email);
    }

    [LocalFact]
    public void RawGitConfig()
    {
        string? email = null;
        string? cfg = null;

        string? ReadEmail(string? path)
        {
            if (string.IsNullOrEmpty(path) ||
                !File.Exists(path))
                return default;

            // Read the user.email value from the .git config file
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("[user]"))
                {
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        var pair = lines[j];
                        if (pair.Trim().Split('=') is string[] parts &&
                            parts.Length == 2 &&
                            parts[0].Trim() == "email")
                            return parts[1].Trim();
                    }
                }
            }

            return default;
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (dir != null)
        {
            if (Path.Combine(dir.FullName, ".git", "config") is string path &&
                File.Exists(path))
            {
                cfg = path;
                break;
            }

            dir = dir.Parent;
        }

        Skip.If(cfg == null, "No git repository found.");

        if ((email = ReadEmail(cfg)) != null)
        {
            Output.WriteLine(email);
            return;
        }

        if ((email = ReadEmail(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gitconfig"))) != null)
        {
            Output.WriteLine(email);
            return;
        }

        Assert.Fail("Should have exited before by rendering an email.");
    }

    // Showcases how to read the user's email using an external process
    [LocalFact]
    public void GitConfig()
    {
        var proc = Process.Start(new ProcessStartInfo("git", "config --get user.email")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        });

        proc!.WaitForExit();

        Skip.If(proc.ExitCode != 0, "Could not run git.");

        var email = proc.StandardOutput.ReadToEnd().Trim();
        Assert.Contains("@", email);
        Output.WriteLine(email);
    }
}
