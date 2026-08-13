namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParsePrimary()
    {
        ExpressionToken token = Advance();
        if (token.Kind == ExpressionTokenKind.Variable)
        {
            return new VariableExpressionSyntax(token.Text[1..^1], token.Span);
        }
        if (token.Kind is ExpressionTokenKind.Word or ExpressionTokenKind.Structured or ExpressionTokenKind.String)
        {
            return new LiteralExpressionSyntax(token.Text, token.Span);
        }
        throw Error($"Expected expression, found '{token.Text}'.", token);
    }
}
