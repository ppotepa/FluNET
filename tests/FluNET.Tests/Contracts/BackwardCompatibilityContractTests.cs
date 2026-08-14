using FluNET.Compilation;
using FluNET.Compilation.Dependencies;
using FluNET.Context;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Tests.Contracts;

[TestFixture]
public sealed class BackwardCompatibilityContractTests
{
    [Test]
    public void CanonicalGetThenSayRetainsFrameIdsAndSequenceConnector()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        ProcessedPrompt prompt = new(
            "GET [text] FROM {input.txt} THEN SAY [text]",
            language.Grammar);
        IReadOnlyList<BoundCommand> commands = new SemanticCommandBinder(language)
            .BindProgram(prompt.Syntax);

        Assert.Multiple(() =>
        {
            Assert.That(commands.Select(command => command.Frame.Id.Value),
                Is.EqualTo(new[] { "core.get.text", "core.say.text" }));
            Assert.That(prompt.Syntax.Links, Has.Count.EqualTo(1));
            Assert.That(prompt.Syntax.Links[0].Kind, Is.EqualTo(CommandLinkKind.Sequence));
        });
    }

    [Test]
    public void CompactJsonLoadRetainsCanonicalConfigFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface("LOAD post.json AS post");

        Assert.That(compilation.IsValid, Is.True, Diagnostics(compilation));
        Assert.That(compilation.BoundProgram!.Commands.Single().Frame.Id.Value,
            Is.EqualTo("core.load.config"));
    }

    [Test]
    public void CommaAndSemicolonRetainCoordinationAndNeutralBoundarySemantics()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "LOAD post.json, todo.json; SAY \"{post.title} — {todo.title}\"");

        Assert.That(compilation.IsValid, Is.True, Diagnostics(compilation));
        Assert.Multiple(() =>
        {
            Assert.That(compilation.Lowering.CanonicalSyntax.Commands, Has.Count.EqualTo(3));
            Assert.That(compilation.DependencyGraph!.Incoming(1), Is.Empty,
                "semicolon/comma must not force an ordering edge between independent reads");
            Assert.That(compilation.DependencyGraph.Incoming(2)
                .Where(edge => edge.Kind == DependencyKind.Data)
                .Select(edge => edge.From),
                Is.EquivalentTo(new[] { 0, 1 }));
        });
    }

    [Test]
    public void CompactPipelineRetainsFrameIdsAndDataflowDependencies()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "GET https://api.example.test/posts AS posts | FILTER userId == 1 | TAKE 5");

        Assert.That(compilation.IsValid, Is.True, Diagnostics(compilation));
        Assert.Multiple(() =>
        {
            Assert.That(compilation.BoundProgram!.Commands.Select(command => command.Frame.Id.Value),
                Is.EqualTo(new[]
                {
                    "surface.get.http.json",
                    "surface.data.filter.json",
                    "surface.data.take.json"
                }));
            Assert.That(compilation.DependencyGraph!.Incoming(1).Select(edge => edge.From), Does.Contain(0));
            Assert.That(compilation.DependencyGraph.Incoming(2).Select(edge => edge.From), Does.Contain(1));
        });
    }

    [Test]
    public void ExplicitCanonicalAndStillOverridesArtificialOrdering()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        ProcessedPrompt prompt = new("SAY first AND SAY second", language.Grammar);
        IReadOnlyList<BoundCommand> commands = new SemanticCommandBinder(language).BindProgram(prompt.Syntax);
        BoundProgram program = new(
            new FluNetProgram(prompt),
            commands.Select(command => new BoundCommandStatement(command)));

        DependencyGraph graph = new DependencyAnalyzer().Analyze(program, prompt.Syntax);

        Assert.That(graph.Edges.Any(edge => edge.From == 0 && edge.To == 1), Is.False);
    }

    [Test]
    public void ProductionReadinessDoesNotSilentlyPromotePublishedLanguageIdentity()
    {
        Assert.That(StandardLanguageIdentity.Version.Value, Is.EqualTo("0.3"));
    }

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
}
