namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParseAdditive()
    {
        ExpressionSyntax left = ParseMultiplicative();
        while (MatchOperator("+", "-"))
        {
            ExpressionToken operation = Previous;
            ExpressionSyntax right = ParseMultiplicative();
            left = Binary(left, operation, right);
        }
        return left;
    }

    private ExpressionSyntax ParseMultiplicative()
    {
        ExpressionSyntax left = ParseUnary();
        while (MatchOperator("*", "/"))
        {
            ExpressionToken operation = Previous;
            ExpressionSyntax right = ParseUnary();
            left = Binary(left, operation, right);
        }
        return left;
    }
}
