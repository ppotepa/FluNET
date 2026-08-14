using FluNET.Compilation;
using FluNET.Compilation.Dependencies;
using FluNET.Compilation.Lowering;
using FluNET.Execution.Planning;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class DependencyAnalyzerTests
{
    [Test]
    public void CompactLinesDeriveParallelReadsAndJoinFromInterpolation()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        SourceDocument source = new("LOAD post.json\nLOAD todo.json\nSAY \"{post.title} — {todo.title}\"");
        LoweringResult lowered = new SurfaceLowerer().Lower(
            new SurfaceParser().Parse(source), language.Grammar, language);
        BoundProgram program = Bind(source.Text, lowered.CanonicalSyntax, language);

        DependencyGraph graph = new DependencyAnalyzer().Analyze(program, lowered.CanonicalSyntax, lowered.InferenceTrace);
        ExecutionPlan plan = new ExecutionPlanner().Create(graph);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.CanonicalSyntax.Links, Is.Empty);
            Assert.That(graph.Edges.Any(edge => edge.From == 0 && edge.To == 1), Is.False);
            Assert.That(graph.Edges.Any(edge => edge.From == 0 && edge.To == 2 && edge.Kind == DependencyKind.Data && edge.Variable == "post"), Is.True);
            Assert.That(graph.Edges.Any(edge => edge.From == 1 && edge.To == 2 && edge.Kind == DependencyKind.Data && edge.Variable == "todo"), Is.True);
            Assert.That(plan.Steps[0].Dependencies, Is.Empty);
            Assert.That(plan.Steps[1].Dependencies, Is.Empty);
            Assert.That(plan.Steps[2].Dependencies.Select(item => item.PredecessorIndex), Is.EquivalentTo(new[] { 0, 1 }));
        });
    }

    [Test]
    public void OrderedEffectsStayOrderedWithoutDataFlow()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        SourceDocument source = new("SAY first\nSAY second");
        LoweringResult lowered = new SurfaceLowerer().Lower(
            new SurfaceParser().Parse(source), language.Grammar, language);
        BoundProgram program = Bind(source.Text, lowered.CanonicalSyntax, language);

        DependencyGraph graph = new DependencyAnalyzer().Analyze(program, lowered.CanonicalSyntax);

        Assert.That(graph.Edges,
            Does.Contain(new DependencyEdge(0, 1, DependencyKind.Effect)));
    }

    [Test]
    public void ExplicitCanonicalAndPreservesParallelOverride()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        ProcessedPrompt prompt = new ProcessedPrompt("SAY first AND SAY second").WithGrammar(language.Grammar);
        BoundProgram program = Bind(prompt.SourceText, prompt.Syntax, language);

        DependencyGraph graph = new DependencyAnalyzer().Analyze(program, prompt.Syntax);

        Assert.That(graph.Edges.Any(edge => edge.From == 0 && edge.To == 1), Is.False);
    }

    private static BoundProgram Bind(string source, PromptSyntax syntax, LanguageSnapshot language)
    {
        IReadOnlyList<BoundCommand> commands = new SemanticCommandBinder(language).BindProgram(syntax);
        FluNetProgram program = new(new ProcessedPrompt(source, language.Grammar));
        return new BoundProgram(program, commands.Select(command => new BoundCommandStatement(command)));
    }
}
