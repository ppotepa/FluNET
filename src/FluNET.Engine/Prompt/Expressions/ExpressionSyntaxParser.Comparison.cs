namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParseEquality()
    {
        ExpressionSyntax left = ParseComparison();
        while (MatchOperator("==", "!="))
        {
            ExpressionToken operation = Previous;
            ExpressionSyntax right = ParseComparison();
            left = Binary(left, operation, right);
        }
        return left;
    }

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
