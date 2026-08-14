using FluNET.Compilation;
using FluNET.Context;
using FluNET.Prompt.Surface;
using FluNET.Tooling;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfacePipelineTests
{
    [Test]
    public void ParserBuildsExplicitPipelineWithoutSplittingQuotedPipes()
    {
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(
            "GET https://api.example.test/post AS post | SAY"));

        Assert.That(parsed.IsValid, Is.True);
        Assert.That(parsed.Program.Statements.Single(), Is.TypeOf<SurfacePipelineSyntax>());
        SurfacePipelineSyntax pipeline = (SurfacePipelineSyntax)parsed.Program.Statements.Single();
        Assert.Multiple(() =>
        {
            Assert.That(pipeline.Stages, Has.Count.EqualTo(2));
            Assert.That(pipeline.Stages[0].NormalizedName, Is.EqualTo("GET"));
            Assert.That(pipeline.Stages[1].NormalizedName, Is.EqualTo("SAY"));
        });
    }

    [Test]
    public void ExplicitPipelineLowersPreviousOutputAsNormalVariableDependency()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "GET https://api.example.test/post AS post | SAY");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Lowering.CanonicalSyntax.Commands, Has.Count.EqualTo(2));
            Assert.That(result.Lowering.CanonicalSyntax.Commands[1].Arguments.Single().Text,
                Is.EqualTo("[post]"));
            Assert.That(result.DependencyGraph!.Incoming(1).Any(edge =>
                edge.From == 0 && edge.Variable == "post"), Is.True);
        });
    }

    [Test]
    public void EmptySayOnFollowingLineConsumesPreviousProducedValue()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
GET https://api.example.test/post AS post
SAY
""";
        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Lowering.CanonicalSyntax.Commands[1].Arguments.Single().Text,
                Is.EqualTo("[post]"));
            Assert.That(result.Plan!.Steps[1].Dependencies.Select(item => item.PredecessorIndex),
                Is.EquivalentTo(new[] { 0 }));
        });
    }

    [Test]
    public void FormatterPreservesPipelineAsOneNormalizedLine()
    {
        string formatted = new SurfaceFormatter().Format(
            "get https://api.example.test/post as post|say");

        Assert.That(formatted,
            Is.EqualTo("GET https://api.example.test/post AS post | SAY"));
    }

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
}
