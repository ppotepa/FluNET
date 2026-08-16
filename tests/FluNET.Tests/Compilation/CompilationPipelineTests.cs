using FluNET.Compilation;
using FluNET.Context;
using FluNET.Prompt;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class CompilationPipelineTests
{
    [Test]
    public void Analyze_ReturnsCanonicalProgramBoundProgramAndPlan()
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(new ProcessedPrompt("SAY hello."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompilationSuccessful, Is.True);
            Assert.That(result.FailedPhase, Is.Null);
            Assert.That(result.DiagnosticBag, Is.Empty);
            Assert.That(result.Program.SourceText, Is.EqualTo("SAY hello."));
            Assert.That(result.Program.Syntax.Commands, Has.Count.EqualTo(1));
            Assert.That(result.BoundProgram, Is.Not.Null);
            Assert.That(result.BoundProgram!.Statements, Has.Count.EqualTo(1));
            Assert.That(result.BoundProgram.Statements[0], Is.TypeOf<BoundCommandStatement>());
            Assert.That(result.BoundCommands, Has.Count.EqualTo(1));
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Plan, Is.Not.Null);
        });
    }

    [Test]
    public void Analyze_PreservesPromptAnalysisSourceCompatibility()
    {
        using FluNETContext context = FluNETContext.Create();

        PromptAnalysis analysis = context.GetEngine().Analyze(new ProcessedPrompt("SAY compatible."));

        Assert.Multiple(() =>
        {
            Assert.That(analysis.IsValid, Is.True);
            Assert.That(analysis.BoundCommands, Has.Count.EqualTo(1));
            Assert.That(analysis.Plan, Is.Not.Null);
        });
    }

    [Test]
    public void Analyze_StopsAtParseAndPreservesStableLexerDiagnostic()
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(new ProcessedPrompt("SAY [broken."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompilationSuccessful, Is.False);
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.Parse));
            Assert.That(result.BoundProgram, Is.Null);
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.DiagnosticBag.Select(diagnostic => diagnostic.Code), Does.Contain("FLN003"));
            Assert.That(result.DiagnosticBag.All(diagnostic => diagnostic.Phase == CompilationPhase.Parse), Is.True);
        });
    }

    [Test]
    public void Analyze_ReportsBindingFailureWithSourceSpan()
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(new ProcessedPrompt("MISSING value."));

        CompilationDiagnostic diagnostic = result.DiagnosticBag.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompilationSuccessful, Is.False);
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.Bind));
            Assert.That(result.BoundProgram, Is.Null);
            Assert.That(result.Plan, Is.Null);
            Assert.That(diagnostic.Code, Is.EqualTo(CompilationDiagnosticCodes.BindingFailure));
            Assert.That(diagnostic.Phase, Is.EqualTo(CompilationPhase.Bind));
            Assert.That(diagnostic.Span.Start, Is.EqualTo(0));
            Assert.That(diagnostic.Span.Length, Is.GreaterThan(0));
        });
    }

    [Test]
    public void DiagnosticBag_TracksSeverityAndInsertionOrder()
    {
        DiagnosticBag diagnostics = new();
        diagnostics.Add(
            "FLN900",
            CompilationPhase.Parse,
            "Informational diagnostic.",
            new SourceSpan(0, 1),
            CompilationDiagnosticSeverity.Info);
        diagnostics.Add(
            "FLN901",
            CompilationPhase.Validate,
            "Error diagnostic.",
            new SourceSpan(2, 1));

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Select(diagnostic => diagnostic.Code),
                Is.EqualTo(new[] { "FLN900", "FLN901" }));
            Assert.That(diagnostics.HasErrors, Is.True);
        });
    }
}

