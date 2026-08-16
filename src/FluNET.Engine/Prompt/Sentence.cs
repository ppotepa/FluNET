namespace FluNET.Prompt;

/// <summary>
/// A source-level instruction boundary. A sentence is intentionally a
/// pre-AST object: it preserves the original text and location while the
/// parser decides whether it is a command, pipeline, or block header.
/// </summary>
public sealed record Sentence(
    int Index,
    string Text,
    SourceSpan Span,
    int Indentation,
    SentenceTerminator Terminator);

public enum SentenceTerminator
{
    EndOfFile,
    NewLine,
    Semicolon,
    Period
}

public static class SentenceSegmenter
{
    public static IReadOnlyList<Sentence> Segment(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<Sentence> sentences = [];
        int segmentStart = -1;
        int lineStart = 0;
        int indentation = 0;
        int depth = 0;
        char? quote = null;
        bool escaped = false;

        void Start(int index)
        {
            if (segmentStart >= 0)
                return;

            segmentStart = lineStart;
            indentation = 0;
            while (segmentStart < source.Length && source[segmentStart] is ' ' or '\t')
            {
                indentation += source[segmentStart] == '\t' ? 4 : 1;
                segmentStart++;
            }
        }

        void Flush(int end, SentenceTerminator terminator)
        {
            if (segmentStart < 0)
                return;

            int right = end;
            while (right > segmentStart && char.IsWhiteSpace(source[right - 1]))
                right--;

            if (right > segmentStart)
            {
                sentences.Add(new Sentence(
                    sentences.Count,
                    source[segmentStart..right],
                    new SourceSpan(segmentStart, right - segmentStart),
                    indentation,
                    terminator));
            }

            segmentStart = -1;
            indentation = 0;
        }

        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];

            if (quote is not null)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (current == '\\')
                {
                    escaped = true;
                }
                else if (current == quote)
                {
                    quote = null;
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                Start(index);
                quote = current;
                continue;
            }

            if (current is '(' or '[' or '{')
            {
                Start(index);
                depth++;
                continue;
            }

            if (current is ')' or ']' or '}')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (current == ';' && depth == 0)
            {
                Flush(index, SentenceTerminator.Semicolon);
                lineStart = index + 1;
                continue;
            }

            if (current == '.' && depth == 0 &&
                (index == source.Length - 1 || char.IsWhiteSpace(source[index + 1])))
            {
                Flush(index, SentenceTerminator.Period);
                lineStart = index + 1;
                continue;
            }

            if (current == '\n' && depth == 0)
            {
                Flush(index, SentenceTerminator.NewLine);
                lineStart = index + 1;
                continue;
            }

            if (!char.IsWhiteSpace(current))
                Start(index);
        }

        Flush(source.Length, SentenceTerminator.EndOfFile);
        return sentences;
    }
}
