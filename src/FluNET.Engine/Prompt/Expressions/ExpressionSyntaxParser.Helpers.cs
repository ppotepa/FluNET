namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private bool MatchWord(string word)
    {
        if (Current.Kind == ExpressionTokenKind.Word &&
            Current.Text.Equals(word, StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool MatchOperator(params string[] values)
    {
        if (Current.Kind == ExpressionTokenKind.Operator &&
            values.Contains(Current.Text, StringComparer.Ordinal))
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool MatchWordOperator(string first, string? second = null)
    {
        if (Current.Kind != ExpressionTokenKind.Word ||
            !Current.Text.Equals(first, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (second is not null &&
            (Peek(1).Kind != ExpressionTokenKind.Word ||
             !Peek(1).Text.Equals(second, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        Advance();
        if (second is not null) Advance();
        return true;
    }

    private bool MatchPunctuation(string value)
    {
        if (Current.Kind == ExpressionTokenKind.Punctuation && Current.Text == value)
        {
            Advance();
            return true;
        }
        return false;
    }

    private ExpressionToken ConsumePunctuation(string value, string message) =>
        MatchPunctuation(value) ? Previous : throw Error(message);

    private ExpressionToken ConsumeWord(string message)
    {
        if (Current.Kind is ExpressionTokenKind.Word or ExpressionTokenKind.String)
        {
            return Advance();
        }
        throw Error(message);
    }

    private ExpressionToken Advance()
    {
        ExpressionToken current = Current;
        if (current.Kind != ExpressionTokenKind.End)
        {
            _position++;
        }
        return current;
    }

    private ExpressionToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
    private ExpressionToken Previous => _tokens[Math.Max(0, _position - 1)];
    private ExpressionToken Peek(int offset) => _tokens[Math.Min(_position + offset, _tokens.Count - 1)];

    private FormatException Error(string message, ExpressionToken? token = null)
    {
        ExpressionToken location = token ?? Current;
        return new FormatException($"{message} At expression position {location.Span.Start}.");
    }
}
