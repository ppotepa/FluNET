using FluNET.Execution.Commands;
using FluNET.Prompt.Expressions;
using FluNET.Variables;

namespace FluNET.Tests.Prompt;

[TestFixture]
public sealed class ExpressionLanguageContractTests
{
    [Test]
    public void ParserHonorsBooleanComparisonAndArithmeticPrecedence()
    {
        ExpressionSyntax syntax = ExpressionSyntaxParser.Parse(
            "1 + 2 * 3 == 7 AND NOT false");

        BinaryExpressionSyntax conjunction =
            Assert.IsInstanceOf<BinaryExpressionSyntax>(syntax);
        BinaryExpressionSyntax equality =
            Assert.IsInstanceOf<BinaryExpressionSyntax>(conjunction.Left);
        BinaryExpressionSyntax addition =
            Assert.IsInstanceOf<BinaryExpressionSyntax>(equality.Left);
        BinaryExpressionSyntax multiplication =
            Assert.IsInstanceOf<BinaryExpressionSyntax>(addition.Right);
        UnaryExpressionSyntax negation =
            Assert.IsInstanceOf<UnaryExpressionSyntax>(conjunction.Right);

        Assert.Multiple(() =>
        {
            Assert.That(conjunction.Operator, Is.EqualTo("AND"));
            Assert.That(equality.Operator, Is.EqualTo("=="));
            Assert.That(addition.Operator, Is.EqualTo("+"));
            Assert.That(multiplication.Operator, Is.EqualTo("*"));
            Assert.That(negation.Operator, Is.EqualTo("NOT"));
        });
    }

    [Test]
    public void ParserBuildsCollectionPropertyAndIndexNodes()
    {
        ObjectExpressionSyntax value = Assert.IsInstanceOf<ObjectExpressionSyntax>(
            ExpressionSyntaxParser.Parse(
                "OBJECT(name: 'Ada', flags: LIST(true, false))"));
        IndexExpressionSyntax access = Assert.IsInstanceOf<IndexExpressionSyntax>(
            ExpressionSyntaxParser.Parse("[user].roles[0]"));

        Assert.Multiple(() =>
        {
            Assert.That(value.Fields, Has.Count.EqualTo(2));
            Assert.That(value.Fields[1].Value, Is.TypeOf<ListExpressionSyntax>());
            Assert.That(access.Target, Is.TypeOf<PropertyExpressionSyntax>());
            Assert.That(access.Index, Is.TypeOf<LiteralExpressionSyntax>());
        });
    }

    [Test]
    public void StandaloneEqualsIsRejectedWithoutScannerLoop()
    {
        Assert.That(
            () => ExpressionSyntaxParser.Parse("1 = 2"),
            Throws.TypeOf<FormatException>());
    }

    [Test]
    public void CompiledLiteralConditionUsesTheSameExpressionTree()
    {
        ExpressionSyntax syntax = ExpressionSyntaxParser.Parse(
            "1 + 2 * 3 == 7 AND NOT false");
        CompiledCondition condition = new ConditionExpressionCompiler().Compile(syntax);

        bool result = condition.Expression.Evaluate(
            new ExpressionEvaluationContext(new EmptyVariables()));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(condition.VariableReferences, Is.Empty);
        });
    }

    private sealed class EmptyVariables : IVariableResolver
    {
        public T? Resolve<T>(string tokenValue) => default;
        public void Register<T>(string name, T value) { }
        public bool IsRegistered(string name) => false;
        public void Clear() { }
        public IEnumerable<string> GetVariableNames() => Array.Empty<string>();
    }
}
