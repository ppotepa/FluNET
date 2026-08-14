using System.Text;

namespace FluNET.Prompt.Surface;

/// <summary>
/// Lightweight line-oriented parser for the compact source layer. It only
/// recognizes statement boundaries, a leading command word, comma-separated
/// values, and an optional AS alias. Meaning belongs to inference/lowering.
/// </summary>
public sealed class SurfaceParser
{
    public SurfaceParseResult Parse(SourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<SurfaceStatementSyntax> statements = [];
        List<SurfaceDiagnostic> diagnostics = [];

        foreach ((string line, int start) in Lines(document.Text))
        {
            ParseLine(line, start, statements, diagnostics);
        }

        SourceSpan span = statements.Count == 0
            ? default
            : SourceSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new SurfaceParseResult(
            document,
            new SurfaceProgramSyntax(statements, span),
            diagnostics);
    }

    private static void ParseLine(
        string line,
        int lineStart,
        ICollection<SurfaceStatementSyntax> statements,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        int leading = 0;
        while (leading < line.Length && char.IsWhiteSpace(line[leading])) leading++;
        int trailing = line.Length;
        while (trailing > leading && char.IsWhiteSpace(line[trailing - 1])) trailing--;
        if (leading == trailing || line[leading] == '#') return;

        string text = line[leading..trailing];
        int absoluteStart = lineStart + leading;
        int verbEnd = 0;
        while (verbEnd < text.Length && !char.IsWhiteSpace(text[verbEnd])) verbEnd++;
        string verb = text[..verbEnd];
        if (!verb.All(character => char.IsLetter(character) || character is '_' or '-'))
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN200",
                $"Invalid surface command name '{verb}'.",
                new SourceSpan(absoluteStart, verb.Length)));
            return;
        }

        string tail = verbEnd < text.Length ? text[verbEnd..].Trim() : string.Empty;
        int tailOffset = tail.Length == 0 ? text.Length : text.IndexOf(tail, StringComparison.Ordinal);
        (string valuesSource, string? alias, int aliasOffset) = SplitAlias(tail);
        List<SurfaceValueSyntax> values = SplitValues(valuesSource, absoluteStart + tailOffset, diagnostics);
        SourceSpan statementSpan = new(absoluteStart, text.Length);
        if (alias is not null && string.IsNullOrWhiteSpace(alias))
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN201",
                "AS must be followed by a non-empty alias.",
                new SourceSpan(absoluteStart + tailOffset + aliasOffset, 2)));
        }

        statements.Add(new SurfaceCommandSyntax(
            verb,
            values,
            string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(),
            statementSpan));
    }

    private static (string Values, string? Alias, int AliasOffset) SplitAlias(string source)
    {
        if (string.IsNullOrEmpty(source)) return (string.Empty, null, -1);
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        for (int index = 0; index < source.Length - 1; index++)
        {
            char current = source[index];
            if (escaped) { escaped = false; continue; }
            if (quote is not null)
            {
                if (current == '\\') escaped = true;
                else if (current == quote) quote = null;
                continue;
            }
            if (current is '"' or '\'') { quote = current; continue; }
            if (current is '(' or '[' or '{') { depth++; continue; }
            if (current is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; }
            if (depth != 0) continue;

            bool starts = (index == 0 || char.IsWhiteSpace(source[index - 1])) &&
                (source[index] is 'A' or 'a') &&
                (source[index + 1] is 'S' or 's') &&
                (index + 2 == source.Length || char.IsWhiteSpace(source[index + 2]));
            if (!starts) continue;

            return (
                source[..index].TrimEnd(),
                index + 2 < source.Length ? source[(index + 2)..].Trim() : string.Empty,
                index);
        }
        return (source, null, -1);
    }

    private static List<SurfaceValueSyntax> SplitValues(
        string source,
        int sourceStart,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceValueSyntax> values = [];
        if (string.IsNullOrWhiteSpace(source)) return values;

        int segmentStart = 0;
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        for (int index = 0; index <= source.Length; index++)
        {
            bool atEnd = index == source.Length;
            char current = atEnd ? '\0' : source[index];
            if (!atEnd)
            {
                if (escaped) { escaped = false; continue; }
                if (quote is not null)
                {
                    if (current == '\\') escaped = true;
                    else if (current == quote) quote = null;
                    continue;
                }
                if (current is '"' or '\'') { quote = current; continue; }
                if (current is '(' or '[' or '{') { depth++; continue; }
                if (current is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; }
            }

            if (!atEnd && (current != ',' || depth != 0)) continue;
            string segment = source[segmentStart..index];
            int left = 0;
            while (left < segment.Length && char.IsWhiteSpace(segment[left])) left++;
            int right = segment.Length;
            while (right > left && char.IsWhiteSpace(segment[right - 1])) right--;
            if (left == right)
            {
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN202",
                    "A compact value cannot be empty.",
                    new SourceSpan(sourceStart + segmentStart, Math.Max(1, segment.Length))));
            }
            else
            {
                values.Add(new SurfaceValueSyntax(
                    segment[left..right],
                    new SourceSpan(sourceStart + segmentStart + left, right - left)));
            }
            segmentStart = index + 1;
        }

        if (quote is not null || depth != 0)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN203",
                "Unclosed quote or delimiter in compact statement.",
                new SourceSpan(sourceStart, source.Length)));
        }
        return values;
    }

    private static IEnumerable<(string Line, int Start)> Lines(string source)
    {
        int start = 0;
        for (int index = 0; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] != '\n') continue;
            int length = index - start;
            if (length > 0 && source[start + length - 1] == '\r') length--;
            yield return (source.Substring(start, length), start);
            start = index + 1;
        }
    }
}
