using FluNET.Compilation;
using FluNET.Context;
using FluNET.Prompt.Surface;
using FluNET.Tooling;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceSeparatorTests
{
    [Test]
    public void CommaCoordinatesValuesWhileSemicolonSeparatesStatements()
    {
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(
            "LOAD post.json, todo.json; SAY \"{post.title} — {todo.title}\""));

        Assert.That(parsed.IsValid, Is.True, Diagnostics(parsed));
        Assert.That(parsed.Program.Statements, Has.Count.EqualTo(2));

        SurfaceCommandSyntax load = (SurfaceCommandSyntax)parsed.Program.Statements[0];
        SurfaceCommandSyntax say = (SurfaceCommandSyntax)parsed.Program.Statements[1];
        Assert.Multiple(() =>
        {
            Assert.That(load.NormalizedName, Is.EqualTo("LOAD"));
            Assert.That(load.Values.Select(value => value.UnquotedText),
                Is.EqualTo(new[] { "post.json", "todo.json" }));
            Assert.That(say.NormalizedName, Is.EqualTo("SAY"));
        });
    }

    [Test]
    public void SemicolonDoesNotSplitQuotedOrNestedContent()
    {
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(
            "SAY \"alpha; beta\"; SAY {left;right}"));

        Assert.That(parsed.IsValid, Is.True, Diagnostics(parsed));
        Assert.That(parsed.Program.Statements, Has.Count.EqualTo(2));

        SurfaceCommandSyntax first = (SurfaceCommandSyntax)parsed.Program.Statements[0];
        SurfaceCommandSyntax second = (SurfaceCommandSyntax)parsed.Program.Statements[1];
        Assert.Multiple(() =>
        {
            Assert.That(first.Values.Single().Text, Is.EqualTo("\"alpha; beta\""));
            Assert.That(second.Values.Single().Text, Is.EqualTo("{left;right}"));
        });
    }

    [Test]
    public void TrailingSemicolonIsAllowedButEmptyInteriorStatementIsRejected()
    {
        SurfaceParseResult terminated = new SurfaceParser().Parse(new SourceDocument("SAY done;"));
        SurfaceParseResult empty = new SurfaceParser().Parse(new SourceDocument("SAY one;; SAY two"));

        Assert.Multiple(() =>
        {
            Assert.That(terminated.IsValid, Is.True, Diagnostics(terminated));
            Assert.That(terminated.Program.Statements, Has.Count.EqualTo(1));
            Assert.That(empty.IsValid, Is.False);
            Assert.That(empty.Diagnostics.Any(item => item.Code == "FLN218"), Is.True);
        });
    }

    [Test]
    public void SemicolonInsideIndentedScopeKeepsTheCurrentScope()
    {
        const string source = """
FROM https://api.example.test
    GET posts AS posts; GET users AS users
""";
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(source));

        Assert.That(parsed.IsValid, Is.True, Diagnostics(parsed));
        SurfaceContextSyntax context = (SurfaceContextSyntax)parsed.Program.Statements.Single();
        Assert.That(context.Statements, Has.Count.EqualTo(2));
        Assert.That(context.Statements.Cast<SurfaceCommandSyntax>().Select(item => item.Alias),
            Is.EqualTo(new[] { "posts", "users" }));
    }

    [Test]
    public void SemicolonDoesNotCreateAnOrderingDependency()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "GET https://api.example.test/posts AS posts; GET https://api.example.test/users AS users");

        Assert.That(result.IsValid, Is.True, Diagnostics(result));
        Assert.Multiple(() =>
        {
            Assert.That(result.DependencyGraph, Is.Not.Null);
            Assert.That(result.DependencyGraph!.Incoming(0), Is.Empty);
            Assert.That(result.DependencyGraph.Incoming(1), Is.Empty);
        });
    }

    [Test]
    public void SemicolonStatementsStillInferDataDependencies()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "LOAD post.json, todo.json; SAY \"{post.title} — {todo.title}\"");

        Assert.That(result.IsValid, Is.True, Diagnostics(result));
        Assert.That(result.Lowering.CanonicalSyntax.Commands, Has.Count.EqualTo(3));
        Assert.That(result.DependencyGraph!.Incoming(2).Select(edge => edge.From),
            Is.EquivalentTo(new[] { 0, 1 }));
    }

    [Test]
    public void FormatterNormalizesSemicolonStatementsToSeparateLines()
    {
        string formatted = new SurfaceFormatter().Format(
            "load post.json, todo.json;say \"{post.title} — {todo.title}\";");

        Assert.That(formatted, Is.EqualTo(
            "LOAD post.json, todo.json" + Environment.NewLine +
            "SAY \"{post.title} — {todo.title}\""));
    }

    private static string Diagnostics(SurfaceParseResult result) =>
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
}
