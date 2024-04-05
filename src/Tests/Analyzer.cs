using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Devlooped.Tests;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TestSponsorLink : DiagnosticAnalyzer
{
    static DiagnosticDescriptor descriptor = new("IDE001", "IDE", "IDE", "Design", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        if (IsEditor)
        {
            context.RegisterCompilationAction(c => c.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None)));
        }
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
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<TestSponsorLink, XUnitVerifier>();

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
    [Fact]
    public void LibGitConfig()
    {
        var path = new DirectoryInfo(@"..\..\..\..\..").FullName;
        Output.WriteLine(path);
        var config = LibGit2Sharp.Configuration.BuildFrom(path);
        var email = config.Get<string>("user.email").Value;

        Assert.Contains("@", email);
        Output.WriteLine(email);
    }

    [Fact]
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
    [Fact]
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

        Assert.Equal(0, proc.ExitCode);

        if (proc.ExitCode == 0)
        {
            var email = proc.StandardOutput.ReadToEnd().Trim();
            Assert.Contains("@", email);
            Output.WriteLine(email);
        }
    }
}
