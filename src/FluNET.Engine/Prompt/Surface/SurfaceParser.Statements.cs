namespace FluNET.Prompt.Surface;

public sealed partial class SurfaceParser
{
    private static SurfaceStatementSyntax? ParseLineStatement(
        LineInfo line,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        string trimmed = line.Text.Trim();
        int absoluteStart = line.Start + line.LeadingCharacters;
        IReadOnlyList<(string Text, int Offset)> parts = SplitPipes(
            trimmed,
            diagnostics,
            absoluteStart);
        if (parts.Count == 0)
            return null;
        if (parts.Count == 1)
        {
            return ParseCommand(
                parts[0].Text,
                absoluteStart + parts[0].Offset,
                diagnostics);
        }

        List<SurfaceCommandSyntax> stages = [];
        foreach ((string text, int offset) in parts)
        {
            SurfaceCommandSyntax? stage = ParseCommand(
                text,
                absoluteStart + offset,
                diagnostics);
            if (stage is null)
                continue;
            if (stage.NormalizedName is "FROM" or "FOR" or "POLICY" or "WITH" or "TASK")
            {
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN208",
                    $"{stage.NormalizedName} cannot appear as a pipeline stage.",
                    stage.Span));
                continue;
            }
            stages.Add(stage);
        }

        return stages.Count == 0
            ? null
            : new SurfacePipelineSyntax(stages, new SourceSpan(absoluteStart, trimmed.Length));
    }

    private static SurfaceCommandSyntax? ParseCommand(
        string text,
        int absoluteStart,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        text = NaturalSurfaceSyntax.RewriteCommand(text).Trim();
        if (text.EndsWith(':'))
            text = text[..^1].TrimEnd();

        int verbEnd = 0;
        while (verbEnd < text.Length && !char.IsWhiteSpace(text[verbEnd]))
            verbEnd++;

        string verb = text[..verbEnd];
        if (verb.Length == 0 ||
            !verb.All(character => char.IsLetter(character) || character is '_' or '-'))
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN200",
                $"Invalid surface command name '{verb}'.",
                new SourceSpan(absoluteStart, Math.Max(1, verb.Length))));
            return null;
        }

        string tail = verbEnd < text.Length
            ? text[verbEnd..].Trim()
            : string.Empty;
        int tailOffset = tail.Length == 0
            ? text.Length
            : text.IndexOf(tail, StringComparison.Ordinal);
        (string valuesSource, string? alias, int aliasOffset) = SplitAlias(tail);
        List<SurfaceValueSyntax> values = SplitValues(
            valuesSource,
            absoluteStart + tailOffset,
            diagnostics);
        if (alias is not null && string.IsNullOrWhiteSpace(alias))
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN201",
                "AS must be followed by a non-empty alias.",
                new SourceSpan(absoluteStart + tailOffset + aliasOffset, 2)));
        }

        return new SurfaceCommandSyntax(
            verb,
            values,
            string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(),
            new SourceSpan(absoluteStart, text.Length));
    }

    private static IReadOnlyList<(string Text, int Offset)> SplitPipes(
        string source,
        ICollection<SurfaceDiagnostic> diagnostics,
        int absoluteStart)
    {
        List<(string Text, int Offset)> result = [];
        int segmentStart = 0;
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        for (int index = 0; index <= source.Length; index++)
        {
            bool atEnd = index == source.Length;
            char current = atEnd ? '\0' : source[index];
            if (!atEnd && ConsumeStructuredCharacter(current, ref depth, ref quote, ref escaped))
                continue;
            if (!atEnd && (current != '|' || depth != 0))
                continue;

            AddPipeSegment(
                source,
                segmentStart,
                index,
                absoluteStart,
                diagnostics,
                result);
            segmentStart = index + 1;
        }
        return result;
    }

    private static void AddPipeSegment(
        string source,
        int start,
        int end,
        int absoluteStart,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<(string Text, int Offset)> result)
    {
        string segment = source[start..end];
        int left = 0;
        while (left < segment.Length && char.IsWhiteSpace(segment[left]))
            left++;
        int right = segment.Length;
        while (right > left && char.IsWhiteSpace(segment[right - 1]))
            right--;

        if (left == right)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN209",
                "A pipeline stage cannot be empty.",
                new SourceSpan(absoluteStart + start, Math.Max(1, segment.Length))));
            return;
        }

        result.Add((segment[left..right], start + left));
    }

    private static (string Values, string? Alias, int AliasOffset) SplitAlias(string source)
    {
        if (string.IsNullOrEmpty(source))
            return (string.Empty, null, -1);

        int depth = 0;
        char? quote = null;
        bool escaped = false;
        for (int index = 0; index < source.Length - 1; index++)
        {
            char current = source[index];
            if (ConsumeStructuredCharacter(current, ref depth, ref quote, ref escaped))
                continue;
            if (depth != 0)
                continue;

            bool starts =
                (index == 0 || char.IsWhiteSpace(source[index - 1])) &&
                source[index] is 'A' or 'a' &&
                source[index + 1] is 'S' or 's' &&
                (index + 2 == source.Length || char.IsWhiteSpace(source[index + 2]));
            if (starts)
            {
                return (
                    source[..index].TrimEnd(),
                    index + 2 < source.Length ? source[(index + 2)..].Trim() : string.Empty,
                    index);
            }
        }

        return (source, null, -1);
    }

    private static List<SurfaceValueSyntax> SplitValues(
        string source,
        int sourceStart,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceValueSyntax> values = [];
        if (string.IsNullOrWhiteSpace(source))
            return values;

        int segmentStart = 0;
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        for (int index = 0; index <= source.Length; index++)
        {
            bool atEnd = index == source.Length;
            char current = atEnd ? '\0' : source[index];
            if (!atEnd && ConsumeStructuredCharacter(current, ref depth, ref quote, ref escaped))
                continue;
            if (!atEnd && (current != ',' || depth != 0))
                continue;

            AddValueSegment(
                source,
                segmentStart,
                index,
                sourceStart,
                diagnostics,
                values);
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

    private static void AddValueSegment(
        string source,
        int start,
        int end,
        int sourceStart,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<SurfaceValueSyntax> values)
    {
        string segment = source[start..end];
        int left = 0;
        while (left < segment.Length && char.IsWhiteSpace(segment[left]))
            left++;
        int right = segment.Length;
        while (right > left && char.IsWhiteSpace(segment[right - 1]))
            right--;

        if (left == right)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN202",
                "A compact value cannot be empty.",
                new SourceSpan(sourceStart + start, Math.Max(1, segment.Length))));
            return;
        }

        values.Add(new SurfaceValueSyntax(
            segment[left..right],
            new SourceSpan(sourceStart + start + left, right - left)));
    }

    private static bool ConsumeStructuredCharacter(
        char current,
        ref int depth,
        ref char? quote,
        ref bool escaped)
    {
        if (escaped)
        {
            escaped = false;
            return true;
        }
        if (quote is not null)
        {
            if (current == '\\')
                escaped = true;
            else if (current == quote)
                quote = null;
            return true;
        }
        if (current is '"' or '\'')
        {
            quote = current;
            return true;
        }
        if (current is '(' or '[' or '{')
        {
            depth++;
            return true;
        }
        if (current is ')' or ']' or '}')
        {
            depth = Math.Max(0, depth - 1);
            return true;
        }
        return false;
    }
}
