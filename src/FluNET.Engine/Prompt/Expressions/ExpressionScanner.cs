namespace FluNET.Prompt.Expressions;

internal enum ExpressionTokenKind
{
    Word,
    Variable,
    Structured,
    String,
    Operator,
    Punctuation,
    End
}

internal readonly record struct ExpressionToken(
    string Text,
    ExpressionTokenKind Kind,
    SourceSpan Span);

internal static class ExpressionScanner
{
    public static IReadOnlyList<ExpressionToken> Scan(string source, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<ExpressionToken> tokens = [];
        int index = 0;
        while (index < source.Length)
        {
            if (char.IsWhiteSpace(source[index]))
            {
                index++;
                continue;
            }

            int start = index;
            char current = source[index];
            if (current is '"' or '\'')
            {
                char quote = current;
                index++;
                bool escaped = false;
                while (index < source.Length)
                {
                    char value = source[index++];
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (value == '\\')
                    {
                        escaped = true;
                    }
                    else if (value == quote)
                    {
                        break;
                    }
                }
                Add(tokens, source, start, index, ExpressionTokenKind.String, offset);
                continue;
            }

            if (current == '[')
            {
                index = ScanBalanced(source, index, '[', ']');
                Add(tokens, source, start, index, ExpressionTokenKind.Variable, offset);
                continue;
            }

            if (current == '{')
            {
                index = ScanBalanced(source, index, '{', '}');
                Add(tokens, source, start, index, ExpressionTokenKind.Structured, offset);
                continue;
            }

            if (index + 1 < source.Length)
            {
                string pair = source.Substring(index, 2);
                if (pair is "==" or "!=" or "<=" or ">=")
                {
                    index += 2;
                    Add(tokens, source, start, index, ExpressionTokenKind.Operator, offset);
                    continue;
                }
            }

            if (current is '+' or '-' or '*' or '/' or '<' or '>' or '!')
            {
                index++;
                Add(tokens, source, start, index, ExpressionTokenKind.Operator, offset);
                continue;
            }

            if (current is '(' or ')' or ',' or '.' or ':')
            {
                index++;
                Add(tokens, source, start, index, ExpressionTokenKind.Punctuation, offset);
                continue;
            }

            while (index < source.Length && !char.IsWhiteSpace(source[index]) &&
                source[index] is not '(' and not ')' and not ',' and not '.' and not ':' and
                not '+' and not '-' and not '*' and not '/' and not '<' and not '>' and not '!' and not '=')
            {
                index++;
            }
            Add(tokens, source, start, index, ExpressionTokenKind.Word, offset);
        }

        tokens.Add(new ExpressionToken(string.Empty, ExpressionTokenKind.End, new SourceSpan(offset + source.Length, 0)));
        return tokens;
    }

    private static int ScanBalanced(string source, int index, char opening, char closing)
    {
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        while (index < source.Length)
        {
            char current = source[index++];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (quote is not null)
            {
                if (current == '\\') escaped = true;
                else if (current == quote) quote = null;
                continue;
            }
            if (current is '"' or '\'')
            {
                quote = current;
                continue;
            }
            if (current == opening) depth++;
            else if (current == closing && --depth == 0) return index;
        }
        throw new FormatException($"Unclosed expression delimiter '{opening}'.");
    }

    private static void Add(
        ICollection<ExpressionToken> tokens,
        string source,
        int start,
        int end,
        ExpressionTokenKind kind,
        int offset) =>
        tokens.Add(new ExpressionToken(
            source[start..end],
            kind,
            new SourceSpan(offset + start, end - start)));
}
