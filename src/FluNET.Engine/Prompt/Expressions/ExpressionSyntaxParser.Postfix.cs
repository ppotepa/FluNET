namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParsePostfix()
    {
        ExpressionSyntax expression = ParsePrimary();
        while (true)
        {
            if (MatchPunctuation("."))
            {
                ExpressionToken property = ConsumeWord("Expected a property name after '.'.");
                expression = new PropertyExpressionSyntax(
                    expression,
                    property.Text,
                    SourceSpan.FromBounds(expression.Span.Start, property.Span.End));
                continue;
            }

            if (Current.Kind == ExpressionTokenKind.Variable)
            {
                ExpressionToken index = Advance();
                string inner = index.Text.Length >= 2 ? index.Text[1..^1] : string.Empty;
                ExpressionSyntax indexExpression = IsIdentifier(inner)
                    ? new VariableExpressionSyntax(inner, index.Span)
                    : new LiteralExpressionSyntax(inner, index.Span);
                expression = new IndexExpressionSyntax(
                    expression,
                    indexExpression,
                    SourceSpan.FromBounds(expression.Span.Start, index.Span.End));
                continue;
            }
            break;
        }
        return expression;
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
}
