using FluNET.Compilation;
using FluNET.Context;
using FluNET.Prompt;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class SemanticValidationTests
{
    [Test]
    public void Analyze_ReportsMissingRequiredRoleFromSelectedFrame()
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(
            new ProcessedPrompt("DOWNLOAD [file] TO {output.txt}."));

        CompilationDiagnostic diagnostic = result.DiagnosticBag.Single(diagnostic =>
            diagnostic.Code == CompilationDiagnosticCodes.MissingRequiredRole);
        Assert.Multiple(() =>
        {
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.Validate));
            Assert.That(diagnostic.Message, Does.Contain("FROM"));
            Assert.That(diagnostic.Span.Length, Is.GreaterThan(0));
        });
    }

    [Test]
    public void Analyze_RejectsMarkerNotDeclaredBySelectedFrame()
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(
            new ProcessedPrompt("SAY hello FROM {input.txt}."));

        CompilationDiagnostic diagnostic = result.DiagnosticBag.Single(diagnostic =>
            diagnostic.Code == CompilationDiagnosticCodes.UnknownMarker);
        Assert.Multiple(() =>
        {
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.Validate));
            Assert.That(diagnostic.Message, Does.Contain("FROM"));
            Assert.That(diagnostic.Span.Start, Is.GreaterThan(0));
        });
    }

    [Test]
    public void Analyze_ReportsDuplicateMarkersWithoutBindingFailure()
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(new ProcessedPrompt(
            "DOWNLOAD [file] FROM https://one.example FROM https://two.example."));

        Assert.Multiple(() =>
        {
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.Validate));
            Assert.That(result.BoundProgram, Is.Not.Null);
            Assert.That(result.DiagnosticBag.Select(diagnostic => diagnostic.Code),
                Does.Contain(CompilationDiagnosticCodes.DuplicateMarker));
            Assert.That(result.DiagnosticBag.Select(diagnostic => diagnostic.Code),
                Does.Not.Contain(CompilationDiagnosticCodes.BindingFailure));
        });
    }

    [Test]
    public void Analyze_ReportsSurplusValuesForSingleValuedMarkedRole()
    {
        using FluNETContext context = FluNETContext.Create();

        CompilationResult result = context.GetEngine().Analyze(
            new ProcessedPrompt("DOWNLOAD [file] FROM https://one.example extra."));

        CompilationDiagnostic diagnostic = result.DiagnosticBag.Single(diagnostic =>
            diagnostic.Code == CompilationDiagnosticCodes.SurplusArgument);
        Assert.Multiple(() =>
        {
            Assert.That(result.FailedPhase, Is.EqualTo(CompilationPhase.Validate));
            Assert.That(diagnostic.Message, Does.Contain("SOURCE").IgnoreCase);
            Assert.That(diagnostic.Span.Length, Is.GreaterThan(0));
        });
    }
}
