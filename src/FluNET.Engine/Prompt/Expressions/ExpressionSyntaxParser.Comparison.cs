namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParseEquality()
    {
        ExpressionSyntax left = ParseComparison();
        while (true)
        {
            ExpressionToken operation;
            if (MatchOperator("==", "!="))
            {
                operation = Previous;
            }
            else if (MatchWordOperator("CONTAINS") ||
                     MatchWordOperator("MATCHES") ||
                     MatchWordOperator("STARTS", "WITH") ||
                     MatchWordOperator("ENDS", "WITH"))
            {
                operation = Previous;
                if (operation.Text.Equals("WITH", StringComparison.OrdinalIgnoreCase))
                {
                    operation = new ExpressionToken(
                        PreviousPrevious.Text + " " + operation.Text,
                        ExpressionTokenKind.Word,
                        SourceSpan.FromBounds(PreviousPrevious.Span.Start, operation.Span.End));
                }
            }
            else
            {
                break;
            }

            ExpressionSyntax right = ParseComparison();
            left = Binary(left, operation, right);
        }
        return left;
    }

    private ExpressionToken PreviousPrevious => _tokens[Math.Max(0, _position - 2)];

    private ExpressionSyntax ParseComparison()
    {
        ExpressionSyntax left = ParseAdditive();
        while (MatchOperator("<", "<=", ">", ">="))
        {
            ExpressionToken operation = Previous;
            ExpressionSyntax right = ParseAdditive();
            left = Binary(left, operation, right);
        }
        return left;
    }
}
