using FluNET.Binding;
using FluNET.Language;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Parsing;

namespace FluNET.Tests;

public class ExpressionBindingTests
{
    [Test]
    public void Parser_builds_property_expression_for_variable_path()
    {
        var parser = new ClassicParser(new LanguageRegistry().Snapshot);
        var result = parser.Parse("SAY [user.name]");
        SentenceNode sentence = result.Script!.Pipelines.Single().Sentences.Single();
        Assert.That(sentence.Clauses.Single().Value, Is.TypeOf<PropertyExpression>());
    }

    [Test]
    public void Runtime_evaluator_reads_properties_and_interpolates_variables()
    {
        var evaluator = new ExpressionRuntimeEvaluator();
        var context = new ActivationContext(new Dictionary<string, object?> { ["user"] = new User("Ada") });
        object? property = evaluator.Evaluate(new PropertyExpression(new VariableExpression("user"), "name"), context);
        object? text = evaluator.Evaluate(new InterpolatedStringExpression("Hello [user.name]"), context);
        Assert.That(property, Is.EqualTo("Ada"));
        Assert.That(text, Is.EqualTo("Hello Ada"));
    }

    private sealed record User(string Name);
}
