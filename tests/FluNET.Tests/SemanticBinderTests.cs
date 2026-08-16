using FluNET.Binding;
using FluNET.Language;
using FluNET.Syntax.Ast;

namespace FluNET.Tests;

public class SemanticBinderTests
{
    [Fact]
    public void Binder_binds_classic_get_using_compiled_role_and_constructor_metadata()
    {
        var registry = new LanguageRegistry();
        var binder = new SemanticBinder(registry.Snapshot);
        var sentence = new SentenceNode(
            "GET",
            [
                new ClauseNode(ClauseKind.What, new VariableExpression("text")),
                new ClauseNode(ClauseKind.From, new ReferenceExpression("input.txt"))
            ]);

        BindingResult<BoundSentence> result = binder.BindSentence(sentence);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal("GET", result.Value!.Verb.Text, ignoreCase: true);
        Assert.Equal(typeof(string[]), result.Value.ResultType);
        Assert.Contains(result.Value.Roles, x => x.Descriptor.Kind == ClauseKind.From);
    }

    [Fact]
    public void Binder_reports_unknown_verbs_without_execution()
    {
        var binder = new SemanticBinder(new LanguageRegistry().Snapshot);
        var sentence = new SentenceNode("DOES_NOT_EXIST", []);

        BindingResult<BoundSentence> result = binder.BindSentence(sentence);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, x => x.Code == "FLU2001");
    }
}
