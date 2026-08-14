namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private readonly IReadOnlyList<ExpressionToken> _tokens;
    private int _position;

    private ExpressionSyntaxParser(IReadOnlyList<ExpressionToken> tokens)
    {
        _tokens = tokens;
    }

    public static ExpressionSyntax Parse(string source, int offset = 0)
    {
        ExpressionSyntaxParser parser = new(ExpressionScanner.Scan(source, offset));
        ExpressionSyntax expression = parser.ParseCoalesce();
        if (parser.Current.Kind != ExpressionTokenKind.End)
        {
            throw parser.Error($"Unexpected token '{parser.Current.Text}'.");
        }
        return expression;
    }

    public static ExpressionSyntax Parse(CommandModifierSyntax modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        if (modifier.Kind != CommandModifierKind.Condition)
        {
            throw new ArgumentException("Only condition modifiers contain expression syntax.", nameof(modifier));
        }
        if (modifier.Values.Count == 0)
        {
            throw new FormatException("IF must be followed by a condition expression.");
        }
        string source = string.Join(" ", modifier.Values.Select(token => token.Text));
        return Parse(source, modifier.Values[0].Span.Start);
    }
}
