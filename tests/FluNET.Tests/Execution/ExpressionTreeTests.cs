using FluNET.Execution.Commands;
using FluNET.Matching;
using FluNET.Matching.StringBased;
using FluNET.Variables;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class ExpressionTreeTests
{
    [Test]
    public void ExpressionsComposeWithoutLosingTheirGenericTypes()
    {
        VariableResolver variables = CreateVariables();
        variables.Register("count", 4);
        IExpression<int> variable = new VariableExpression<int>("[count]");
        IExpression<string> formatted = new ConversionExpression<int, string>(
            variable,
            value => $"items:{value}");
        IExpression<int> length = new PropertyExpression<string, int>(formatted, text => text.Length);

        int result = length.Evaluate(new ExpressionEvaluationContext(variables));

        Assert.That(result, Is.EqualTo(7));
    }

    [Test]
    public void ListExpressionEvaluatesChildrenInOneContext()
    {
        IExpression<IReadOnlyList<int>> expression = new ListExpression<int>(
            new IExpression<int>[] { new LiteralExpression<int>(1), new LiteralExpression<int>(2) });

        Assert.That(
            expression.Evaluate(new ExpressionEvaluationContext(CreateVariables())),
            Is.EqualTo(new[] { 1, 2 }));
    }

    private static VariableResolver CreateVariables() => new(new MatcherResolver(
        new IMatcher[]
        {
            new StringVariableMatcher(),
            new StringReferenceMatcher(),
            new StringDestructuringMatcher()
        }));
}
