namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParseUnary()
    {
        if (MatchWord("NOT") || MatchOperator("!", "-"))
        {
            ExpressionToken operation = Previous;
            ExpressionSyntax operand = ParseUnary();
            return new UnaryExpressionSyntax(
                operation.Text.ToUpperInvariant(),
                operand,
                SourceSpan.FromBounds(operation.Span.Start, operand.Span.End));
        }
        return ParsePostfix();
    }
}
