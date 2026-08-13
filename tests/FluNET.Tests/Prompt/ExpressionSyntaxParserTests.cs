using FluNET.Prompt.Expressions;

namespace FluNET.Tests.Prompt;

[TestFixture]
public sealed class ExpressionSyntaxParserTests
{
    [Test]
    public void ParserHonorsArithmeticPrecedence()
    {
        ExpressionSyntax syntax = ExpressionSyntaxParser.Parse("1 + 2 * 3");

        BinaryExpressionSyntax addition = Assert.IsInstanceOf<BinaryExpressionSyntax>(syntax);
        BinaryExpressionSyntax multiplication = Assert.IsInstanceOf<BinaryExpressionSyntax>(addition.Right);

        Assert.Multiple(() =>
        {
            Assert.That(addition.Operator, Is.EqualTo("+"));
            Assert.That(multiplication.Operator, Is.EqualTo("*"));
        });
    }

    [Test]
    public void ParserBuildsParenthesizedBooleanTree()
    {
        ExpressionSyntax syntax = ExpressionSyntaxParser.Parse("([enabled] AND NOT [blocked])");

        ParenthesizedExpressionSyntax grouped =
            Assert.IsInstanceOf<ParenthesizedExpressionSyntax>(syntax);
        BinaryExpressionSyntax conjunction =
            Assert.IsInstanceOf<BinaryExpressionSyntax>(grouped.Expression);
        UnaryExpressionSyntax negation =
            Assert.IsInstanceOf<UnaryExpressionSyntax>(conjunction.Right);

        Assert.Multiple(() =>
        {
            Assert.That(conjunction.Operator, Is.EqualTo("AND"));
            Assert.That(negation.Operator, Is.EqualTo("NOT"));
        });
    }

    [Test]
    public void ParserBuildsCollectionPropertyAndIndexNodes()
    {
        ObjectExpressionSyntax value = Assert.IsInstanceOf<ObjectExpressionSyntax>(
            ExpressionSyntaxParser.Parse("OBJECT(name: 'Ada', flags: LIST(true, false))"));
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
    public void SingleEqualsFailsInsteadOfBeingAcceptedAsEquality()
    {
        Assert.That(
            () => ExpressionSyntaxParser.Parse("1 = 2"),
            Throws.TypeOf<FormatException>());
    }
}
