using FluNET.Automation;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Declarative;
using FluNET.Declarative.Reconciliation;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ShowcaseApplicationTests
{
    [Test]
    public void SurfaceApplicationsCompile()
    {
        string showcase = ShowcaseDirectory();
        string automation = Path.Combine(showcase, "apps", "07-automation-daemon.flu");
        string[] files =
        [
            Path.Combine(showcase, "program.flu"),
            Path.Combine(showcase, "desired-state", "bootstrap.flu"),
            .. Directory.GetFiles(Path.Combine(showcase, "apps"), "*.flu")
                .Where(path => !path.Equals(automation, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        ];

        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        List<string> failures = [];
        foreach (string file in files)
        {
            SourceDocument document = new(File.ReadAllText(file), SourceSyntaxKind.Auto, file);
            SurfaceCompilationResult result = context.GetSurfaceCompiler().Compile(document);
            if (!result.IsValid) failures.Add(Diagnostics(showcase, file, result));
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine + Environment.NewLine, failures));
    }

    [Test]
    public void AutomationApplicationCompiles()
    {
        string file = Path.Combine(ShowcaseDirectory(), "apps", "07-automation-daemon.flu");
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        AutomationCompilationResult result = context.CompileAutomations(File.ReadAllText(file));

        Assert.That(result.IsValid, Is.True,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    [Test]
    public void EnsureApplicationCompiles()
    {
        string file = Path.Combine(ShowcaseDirectory(), "desired-state", "ensure-notes.flu");
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        DesiredStateCompilationResult result = context.CompileEnsure(File.ReadAllText(file));

        Assert.That(result.IsValid, Is.True,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    [Test]
    public void SyncApplicationCompiles()
    {
        string file = Path.Combine(ShowcaseDirectory(), "desired-state", "sync-users.flu");
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncCompilationResult result = context.CompileSync(File.ReadAllText(file));

        Assert.That(result.IsValid, Is.True,
            string.Join(Environment.NewLine, result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
    }

    private static string ShowcaseDirectory() =>
        Path.Combine(RepositoryRoot(), "samples", "FluNET.Showcase");

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FluNET.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException(
            $"Could not find FluNET.sln above '{TestContext.CurrentContext.TestDirectory}'.");
    }

    private static string Diagnostics(string showcase, string file, SurfaceCompilationResult result)
    {
        IEnumerable<string> diagnostics =
            result.SurfaceParse.Diagnostics.Select(item => $"{item.Code} [Surface]: {item.Message}")
                .Concat(result.Lowering.Diagnostics.Select(item => $"{item.Code} [Lowering]: {item.Message}"))
                .Concat(result.Diagnostics.Select(item => $"{item.Code} [{item.Phase}]: {item.Message}"));
        return $"{Path.GetRelativePath(showcase, file)}:{Environment.NewLine}{string.Join(Environment.NewLine, diagnostics)}";
    }
}
