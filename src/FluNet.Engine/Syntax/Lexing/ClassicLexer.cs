using FluNET.Diagnostics;

namespace FluNET.Syntax.Lexing;

public enum ClassicTokenKind
{
    Word,
    Variable,
    Reference,
    String,
    NewLine
}

public sealed record ClassicToken(ClassicTokenKind Kind, string Text, TextSpan Span);

/// <summary>
/// Small lexer for the Classic sentence surface. It deliberately knows nothing about verbs;
/// language knowledge enters at parsing/binding through LanguageSnapshot.
/// </summary>
public sealed class ClassicLexer
{
    public IReadOnlyList<ClassicToken> Lex(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = new List<ClassicToken>();
        int i = 0;

        while (i < source.Length)
        {
            char ch = source[i];
            if (ch == '\r' || ch == '\n' || ch == ';')
            {
                int start = i;
                if (ch == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                i++;
                tokens.Add(new(ClassicTokenKind.NewLine, "\n", new TextSpan(start, i - start)));
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }

            if (ch == '[')
            {
                tokens.Add(ReadDelimited(source, ref i, '[', ']', ClassicTokenKind.Variable));
                continue;
            }

            if (ch == '{')
            {
                tokens.Add(ReadDelimited(source, ref i, '{', '}', ClassicTokenKind.Reference));
                continue;
            }

            if (ch == '"')
            {
                tokens.Add(ReadString(source, ref i));
                continue;
            }

            int wordStart = i;
            while (i < source.Length && !char.IsWhiteSpace(source[i]) && source[i] != ';') i++;
            tokens.Add(new(ClassicTokenKind.Word, source[wordStart..i], new TextSpan(wordStart, i - wordStart)));
        }

        return tokens;
    }

    private static ClassicToken ReadDelimited(string source, ref int index, char open, char close, ClassicTokenKind kind)
    {
        int start = index++;
        int contentStart = index;
        while (index < source.Length && source[index] != close) index++;
        int contentEnd = index;
        if (index < source.Length && source[index] == close) index++;
        return new(kind, source[contentStart..contentEnd], new TextSpan(start, index - start));
    }

    private static ClassicToken ReadString(string source, ref int index)
    {
        int start = index++;
        var value = new System.Text.StringBuilder();
        bool escaped = false;
        while (index < source.Length)
        {
            char ch = source[index++];
            if (escaped)
            {
                value.Append(ch);
                escaped = false;
                continue;
            }
            if (ch == '\\')
            {
                escaped = true;
                continue;
            }
            if (ch == '"') break;
            value.Append(ch);
        }
        return new(ClassicTokenKind.String, value.ToString(), new TextSpan(start, index - start));
    }
}
