using FluNET.Binding;
using FluNET.Language;
using FluNET.Syntax.Ast;

namespace FluNET.Tests;

public class VerbActivatorTests
{
    [Test]
    public void Activator_constructs_classic_get_from_bound_constructor_metadata()
    {
        var registry = new LanguageRegistry();
        var binder = new SemanticBinder(registry.Snapshot);
        var sentence = new SentenceNode("GET", [new ClauseNode(ClauseKind.What, new VariableExpression("text")), new ClauseNode(ClauseKind.From, new ReferenceExpression("input.txt"))]);
        BindingResult<BoundSentence> binding = binder.BindSentence(sentence);
        Assert.That(binding.Success, Is.True);
        var activator = new VerbActivator();
        var verb = activator.Create(binding.Value!);
        Assert.That(verb.Text, Is.EqualTo("GET").IgnoreCase);
        Assert.That(verb.GetType().Name, Is.EqualTo("GetText"));
    }
}
