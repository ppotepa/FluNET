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
TASK fetch-user id -> Json
    GET https://api.example.test/users/{id}
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
