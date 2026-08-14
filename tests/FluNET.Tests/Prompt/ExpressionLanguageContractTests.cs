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

        Assert.That(syntax, Is.TypeOf<BinaryExpressionSyntax>());
        BinaryExpressionSyntax conjunction = (BinaryExpressionSyntax)syntax;
        Assert.That(conjunction.Left, Is.TypeOf<BinaryExpressionSyntax>());
        BinaryExpressionSyntax equality = (BinaryExpressionSyntax)conjunction.Left;
        Assert.That(equality.Left, Is.TypeOf<BinaryExpressionSyntax>());
        BinaryExpressionSyntax addition = (BinaryExpressionSyntax)equality.Left;
        Assert.That(addition.Right, Is.TypeOf<BinaryExpressionSyntax>());
        BinaryExpressionSyntax multiplication = (BinaryExpressionSyntax)addition.Right;
        Assert.That(conjunction.Right, Is.TypeOf<UnaryExpressionSyntax>());
        UnaryExpressionSyntax negation = (UnaryExpressionSyntax)conjunction.Right;

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
        ExpressionSyntax objectSyntax = ExpressionSyntaxParser.Parse(
            "OBJECT(name: 'Ada', flags: LIST(true, false))");
        ExpressionSyntax accessSyntax = ExpressionSyntaxParser.Parse("[user].roles[0]");

        Assert.That(objectSyntax, Is.TypeOf<ObjectExpressionSyntax>());
        Assert.That(accessSyntax, Is.TypeOf<IndexExpressionSyntax>());
        ObjectExpressionSyntax value = (ObjectExpressionSyntax)objectSyntax;
        IndexExpressionSyntax access = (IndexExpressionSyntax)accessSyntax;

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
    public void UnterminatedExpressionStringIsRejected()
    {
        Assert.That(
            () => ExpressionSyntaxParser.Parse("'unterminated"),
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
