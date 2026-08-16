using FluNET.Compilation;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceTaskTests
{
    [Test]
    public void TaskRunExpandsIntoNormalCanonicalCommands()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
TASK fetch-user id RETURNS Json
    GET https://api.example.test/users/{id} AS fetched
    RETURN [fetched]
RUN fetch-user 42 AS user
SAY "{user.id}"
""";
        SurfaceCompilationResult result = context.CompileSurface(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Lowering.CanonicalSyntax.Commands.Select(command => command.Verb.Text), Is.EqualTo(new[] { "GETHTTP", "SAY" }));
            Assert.That(result.Plan!.Steps[1].Dependencies.Select(item => item.PredecessorIndex), Does.Contain(0));
        });
    }

    [Test]
    public void TaskRunCanPublishAnExplicitlyAliasedTextOperation()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
TASK make-label value RETURNS Text
    TRIM "{value}" AS clean
    UPPER [clean] AS normalized
    RETURN [normalized]
RUN make-label reusable-task AS label
SAY "{label}"
""";

        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Lowering.CanonicalSyntax.Commands.Select(command => command.Verb.Text),
                Is.EqualTo(new[] { "TRIMTEXT", "UPPERTEXT", "SAY" }));
            Assert.That(result.Plan!.Steps[2].Dependencies.Select(item => item.PredecessorIndex), Does.Contain(1));
        });
    }

    [Test]
    public void ArrowResultDeclarationIsRejectedInFavorOfReturns()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
TASK fetch-user id -> Json
    GET https://api.example.test/users/{id} AS user
RUN fetch-user 42 AS user
""";

        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.SurfaceParse.Diagnostics.Select(item => item.Code), Does.Contain("FLN290"));
        Assert.That(Diagnostics(result), Does.Contain("RETURNS Type"));
    }

    [Test]
    public void NonUnitTaskMustDeclareAnExplicitReturn()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
TASK missing-result RETURNS Json
    SAY "not a result"
RUN missing-result
""";

        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.SurfaceParse.Diagnostics.Select(item => item.Code), Does.Contain("FLN299"));
    }

    [Test]
    public void RecursiveTaskExpansionIsRejected()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
TASK a
    RUN a
RUN a
""";
        SurfaceCompilationResult result = context.CompileSurface(source);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.SurfaceParse.Diagnostics.Select(item => item.Code), Does.Contain("FLN296"));
    }

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.SurfaceParse.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
}
