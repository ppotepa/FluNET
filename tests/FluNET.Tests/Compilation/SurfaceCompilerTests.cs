using FluNET.Compilation;
using FluNET.Context;
using FluNET.Tooling;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceCompilerTests
{
    [Test]
    public void CompactProgramCompilesThroughTypedDataflowPlanWithoutEffects()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
LOAD post.json
LOAD todo.json
SAY "{post.title} — {todo.title}"
""";

        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.TypedProgram, Is.Not.Null);
            Assert.That(result.DependencyGraph, Is.Not.Null);
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Plan!.Steps, Has.Count.EqualTo(3));
            Assert.That(result.Plan.Steps[0].Dependencies, Is.Empty);
            Assert.That(result.Plan.Steps[1].Dependencies, Is.Empty);
            Assert.That(result.Plan.Steps[2].Dependencies.Select(item => item.PredecessorIndex),
                Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(result.BoundProgram!.Program.Syntax,
                Is.SameAs(result.Lowering.CanonicalSyntax));
        });
    }

    [Test]
    public void CheckReportsProviderRequirementWithoutExecution()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = new SurfaceCheckService(context.GetSurfaceCompiler())
            .Check("GET secret:github-token");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Lowering.Diagnostics.Select(item => item.Code), Does.Contain("FLN234"));
            Assert.That(result.Plan, Is.Null);
        });
    }

    [Test]
    public void MissingInterpolationRootFailsTypeCheckBeforePlanning()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface("SAY \"{missing.title}\"");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.TypeCheck));
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("FLN150"));
            Assert.That(result.Plan, Is.Null);
        });
    }

    [Test]
    public void AutomaticParallelWritesToSameOutputAreRejected()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
LOAD first.json AS value
LOAD second.json AS value
""";
        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.TypeCheck));
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("FLN153"));
        });
    }

    [Test]
    public void FormatterExplainAndGraphShareCompilerArtifacts()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
from https://api.example.test/
  get posts/1 as post
  say "{post.title}"
""";

        string formatted = new SurfaceFormatter().Format(source);
        SurfaceExplanation explanation = new SurfaceExplainService(context.GetSurfaceCompiler()).Explain(formatted);
        string graph = new SurfaceGraphExporter().ToDot(explanation.Compilation);

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.StartWith("FROM https://api.example.test/"));
            Assert.That(formatted, Does.Contain("    GET posts/1 AS post"));
            Assert.That(explanation.Compilation.IsValid, Is.True, explanation.Text);
            Assert.That(explanation.Text, Does.Contain("INFERENCE"));
            Assert.That(explanation.Text, Does.Contain("LOWERING"));
            Assert.That(explanation.Text, Does.Contain("PLAN"));
            Assert.That(graph, Does.Contain("digraph FluNET"));
            Assert.That(graph, Does.Contain("surface.get.http.json"));
        });
    }

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
}
