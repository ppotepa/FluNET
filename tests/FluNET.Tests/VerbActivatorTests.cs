using FluNET.Binding;
using FluNET.Language;
using FluNET.Syntax.Ast;

namespace FluNET.Tests;

public class VerbActivatorTests
{
    [Fact]
    public void Activator_constructs_classic_get_from_bound_constructor_metadata()
    {
        var registry = new LanguageRegistry();
        var binder = new SemanticBinder(registry.Snapshot);
        var sentence = new SentenceNode(
            "GET",
            [
                new ClauseNode(ClauseKind.What, new VariableExpression("text")),
                new ClauseNode(ClauseKind.From, new ReferenceExpression("input.txt"))
            ]);

        BindingResult<BoundSentence> binding = binder.BindSentence(sentence);
        Assert.True(binding.Success);

        var activator = new VerbActivator();
        var verb = activator.Create(binding.Value!);

        Assert.Equal("GET", verb.Text, ignoreCase: true);
        Assert.Equal("GetText", verb.GetType().Name);
    }
}
