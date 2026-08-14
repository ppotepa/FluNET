namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParseList()
    {
        ExpressionToken keyword = Advance();
        ConsumePunctuation("(", "Expected '(' after LIST.");
        List<ExpressionSyntax> items = [];

        if (MatchPunctuation(")"))
        {
            return new ListExpressionSyntax(
                items,
                SourceSpan.FromBounds(keyword.Span.Start, Previous.Span.End));
        }

        while (true)
        {
            items.Add(ParseOr());
            if (MatchPunctuation(")"))
            {
                return new ListExpressionSyntax(
                    items,
                    SourceSpan.FromBounds(keyword.Span.Start, Previous.Span.End));
            }
            ConsumePunctuation(",", "Expected ',' or ')' in LIST expression.");
        }
    }

    private ExpressionSyntax ParseObject()
    {
        ExpressionToken keyword = Advance();
        ConsumePunctuation("(", "Expected '(' after OBJECT.");
        List<ObjectFieldSyntax> fields = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        if (MatchPunctuation(")"))
        {
            return new ObjectExpressionSyntax(
                fields,
                SourceSpan.FromBounds(keyword.Span.Start, Previous.Span.End));
        }

        while (true)
        {
            ExpressionToken name = ConsumeWord("Expected an object field name.");
            if (!names.Add(name.Text))
            {
                throw Error($"Object field '{name.Text}' is declared more than once.", name);
            }
            ConsumePunctuation(":", "Expected ':' after object field name.");
            ExpressionSyntax value = ParseOr();
            fields.Add(new ObjectFieldSyntax(
                name.Text.Trim('"', '\''),
                value,
                SourceSpan.FromBounds(name.Span.Start, value.Span.End)));

            if (MatchPunctuation(")"))
            {
                return new ObjectExpressionSyntax(
                    fields,
                    SourceSpan.FromBounds(keyword.Span.Start, Previous.Span.End));
            }
            ConsumePunctuation(",", "Expected ',' or ')' in OBJECT expression.");
        }
    }

    private bool StartsCall(string name) =>
        Current.Kind == ExpressionTokenKind.Word &&
        Current.Text.Equals(name, StringComparison.OrdinalIgnoreCase) &&
        Peek(1).Kind == ExpressionTokenKind.Punctuation &&
        Peek(1).Text == "(";
}
