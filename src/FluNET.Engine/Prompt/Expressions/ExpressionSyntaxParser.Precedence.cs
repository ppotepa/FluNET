namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParseOr()
    {
        ExpressionSyntax left = ParseAnd();
        while (MatchWord("OR"))
        {
            ExpressionToken operation = Previous;
            ExpressionSyntax right = ParseAnd();
            left = Binary(left, operation, right);
        }
        return left;
    }

    private ExpressionSyntax ParseAnd()
    {
        ExpressionSyntax left = ParsePrimary();
        while (MatchWord("AND"))
        {
            ExpressionToken operation = Previous;
            ExpressionSyntax right = ParsePrimary();
            left = Binary(left, operation, right);
        }
        return left;
    }

    private static BinaryExpressionSyntax Binary(
        ExpressionSyntax left,
        ExpressionToken operation,
        ExpressionSyntax right) =>
        new(
            left,
            operation.Text.ToUpperInvariant(),
            right,
            SourceSpan.FromBounds(left.Span.Start, right.Span.End));
}
