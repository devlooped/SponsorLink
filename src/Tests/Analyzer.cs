using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Devlooped;

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
        var config = LibGit2Sharp.Configuration.BuildFrom(Directory.GetCurrentDirectory());
        var email = config.Get<string>("user.email").Value;

        Assert.Contains("@", email);
        Output.WriteLine(email);
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
