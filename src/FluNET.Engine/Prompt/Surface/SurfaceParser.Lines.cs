namespace FluNET.Prompt.Surface;

public sealed partial class SurfaceParser
{
    /// <summary>
    /// Splits one physical line into neutral compact statements. A top-level ';'
    /// has the same statement-boundary semantics as a newline; it never implies
    /// THEN, AND, or pipeline dataflow. Quotes and nested delimiters protect ';'.
    /// </summary>
    private static IReadOnlyList<LineInfo> SplitStatements(
        LineInfo line,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        string source = line.Text;
        List<LineInfo> result = [];
        int segmentStart = line.LeadingCharacters;
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        bool sawSeparator = false;

        for (int index = line.LeadingCharacters; index < source.Length; index++)
        {
            char current = source[index];
            if (ConsumeStructuredCharacter(current, ref depth, ref quote, ref escaped))
                continue;

            bool period = current == '.' &&
                depth == 0 &&
                (index == source.Length - 1 || char.IsWhiteSpace(source[index + 1]));
            if ((current != ';' && !period) || depth != 0)
                continue;

            sawSeparator = true;
            AddStatementSegment(
                source,
                line,
                segmentStart,
                index,
                allowEmpty: false,
                diagnostics,
                result);
            segmentStart = index + 1;
        }

        if (!sawSeparator)
        {
            int start = line.LeadingCharacters;
            return [new LineInfo(source[start..], line.Start + start, line.Indent, 0)];
        }

        // A final semicolon is a legal terminator. Interior/leading empty
        // statements are diagnosed when their separator is encountered above.
        AddStatementSegment(
            source,
            line,
            segmentStart,
            source.Length,
            allowEmpty: true,
            diagnostics,
            result);
        return result;
    }

    private static void AddStatementSegment(
        string source,
        LineInfo line,
        int start,
        int end,
        bool allowEmpty,
        ICollection<SurfaceDiagnostic> diagnostics,
        ICollection<LineInfo> result)
    {
        int left = start;
        while (left < end && char.IsWhiteSpace(source[left]))
            left++;
        int right = end;
        while (right > left && char.IsWhiteSpace(source[right - 1]))
            right--;

        if (left == right)
        {
            if (!allowEmpty)
            {
                int marker = Math.Clamp(end, 0, Math.Max(0, source.Length - 1));
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN218",
                    "A semicolon must separate two non-empty statements; only a final trailing semicolon may terminate a statement.",
                    new SourceSpan(line.Start + marker, 1)));
            }
            return;
        }

        result.Add(new LineInfo(
            source[left..right],
            line.Start + left,
            line.Indent,
            0));
    }

    private static int FindSentenceIndex(IReadOnlyList<Sentence> sentences, int position) =>
        sentences.FirstOrDefault(sentence =>
            sentence.Span.Start <= position && sentence.Span.End >= position)?.Index ?? -1;

    private static IEnumerable<LineInfo> Lines(string source)
    {
        int start = 0;
        for (int index = 0; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] != '\n')
                continue;

            int length = index - start;
            if (length > 0 && source[start + length - 1] == '\r')
                length--;
            string text = source.Substring(start, length);
            int charIndex = 0;
            int indent = 0;
            while (charIndex < text.Length && text[charIndex] is ' ' or '\t')
            {
                indent += text[charIndex] == '\t' ? 4 : 1;
                charIndex++;
            }

            yield return new LineInfo(text, start, indent, charIndex);
            start = index + 1;
        }
    }

    private sealed record LineInfo(
        string Text,
        int Start,
        int Indent,
        int LeadingCharacters,
        int SentenceIndex = -1);
}
