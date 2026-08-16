using FluNET.Binding;
using FluNET.Language;
using FluNET.Syntax.Ast;

namespace FluNET.Tests;

public class SemanticBinderTests
{
    [Test]
    public void Binder_binds_classic_get_using_compiled_role_and_constructor_metadata()
    {
        var registry = new LanguageRegistry();
        var binder = new SemanticBinder(registry.Snapshot);
        var sentence = new SentenceNode("GET", [new ClauseNode(ClauseKind.What, new VariableExpression("text")), new ClauseNode(ClauseKind.From, new ReferenceExpression("input.txt"))]);
        BindingResult<BoundSentence> result = binder.BindSentence(sentence);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Value, Is.Not.Null);
        Assert.That(result.Value!.Verb.Text, Is.EqualTo("GET").IgnoreCase);
        Assert.That(result.Value.ResultType, Is.EqualTo(typeof(string[])));
        Assert.That(result.Value.Roles.Any(x => x.Descriptor.Kind == ClauseKind.From), Is.True);
    }

    [Test]
    public void Binder_reports_unknown_verbs_without_execution()
    {
        var binder = new SemanticBinder(new LanguageRegistry().Snapshot);
        BindingResult<BoundSentence> result = binder.BindSentence(new SentenceNode("DOES_NOT_EXIST", []));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(x => x.Code == "FLU2001"), Is.True);
    }
}
