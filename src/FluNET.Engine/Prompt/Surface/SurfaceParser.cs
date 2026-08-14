namespace FluNET.Prompt.Surface;

public sealed class SurfaceParser
{
    public SurfaceParseResult Parse(SourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<SurfaceDiagnostic> diagnostics = [];
        LineInfo[] lines = Lines(document.Text)
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && !line.Text.TrimStart().StartsWith('#'))
            .SelectMany(line => SplitStatements(line, diagnostics))
            .ToArray();
        int cursor = 0;
        int rootIndent = lines.Length == 0 ? 0 : lines.Min(line => line.Indent);
        IReadOnlyList<SurfaceStatementSyntax> statements = ParseBlock(lines, ref cursor, rootIndent, diagnostics);
        SourceSpan span = statements.Count == 0 ? default : SourceSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new SurfaceParseResult(document, new SurfaceProgramSyntax(statements, span), diagnostics);
    }

    private static IReadOnlyList<SurfaceStatementSyntax> ParseBlock(IReadOnlyList<LineInfo> lines, ref int cursor, int indent, ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> statements = [];
        while (cursor < lines.Count)
        {
            LineInfo line = lines[cursor];
            if (line.Indent < indent) break;
            if (line.Indent > indent) { diagnostics.Add(new SurfaceDiagnostic("FLN204", "Unexpected indentation. Only block-form surface statements may introduce an indented block.", new SourceSpan(line.Start, Math.Max(1, line.LeadingCharacters)))); cursor++; continue; }
            SurfaceStatementSyntax? parsed = ParseLineStatement(line, diagnostics);
            cursor++;
            if (parsed is null) continue;

            if (parsed is SurfaceCommandSyntax command && command.NormalizedName == "FROM")
            {
                if (command.Values.Count != 1 || command.Alias is not null) { diagnostics.Add(new SurfaceDiagnostic("FLN205", "A FROM context requires exactly one base resource and no AS alias.", command.Span)); continue; }
                if (!TryReadChildBlock(lines, ref cursor, indent, diagnostics, command.Span, "FROM context", out IReadOnlyList<SurfaceStatementSyntax>? children)) continue;
                statements.Add(new SurfaceContextSyntax(command.Values[0], children!, SourceSpan.FromBounds(command.Span.Start, children![^1].Span.End)));
                continue;
            }
            if (parsed is SurfaceCommandSyntax forCommand && forCommand.NormalizedName == "FOR")
            {
                if (!TryReadChildBlock(lines, ref cursor, indent, diagnostics, forCommand.Span, "FOR EACH", out IReadOnlyList<SurfaceStatementSyntax>? children)) continue;
                SourceSpan loopSpan = SourceSpan.FromBounds(forCommand.Span.Start, children![^1].Span.End);
                if (SurfaceForEachDescriptor.TryCreate(forCommand, children, diagnostics, out SurfaceForEachDescriptor? descriptor)) statements.Add(new SurfaceCommandSyntax("FOREACH", [new SurfaceValueSyntax(descriptor!.Encode(), loopSpan)], null, loopSpan));
                continue;
            }
            if (parsed is SurfaceCommandSyntax policy && policy.NormalizedName == "POLICY")
            {
                if (!TrySingleName(policy, "POLICY", diagnostics, out string? name) || !TryReadChildBlock(lines, ref cursor, indent, diagnostics, policy.Span, "POLICY definition", out IReadOnlyList<SurfaceStatementSyntax>? children)) continue;
                statements.Add(new SurfacePolicyDefinitionSyntax(name!, children!, SourceSpan.FromBounds(policy.Span.Start, children![^1].Span.End)));
                continue;
            }
            if (parsed is SurfaceCommandSyntax with && with.NormalizedName == "WITH")
            {
                if (!TrySingleName(with, "WITH", diagnostics, out string? name) || !TryReadChildBlock(lines, ref cursor, indent, diagnostics, with.Span, "WITH policy", out IReadOnlyList<SurfaceStatementSyntax>? children)) continue;
                statements.Add(new SurfacePolicyContextSyntax(name!, children!, SourceSpan.FromBounds(with.Span.Start, children![^1].Span.End)));
                continue;
            }
            if (parsed is SurfaceCommandSyntax task && task.NormalizedName == "TASK")
            {
                if (!TryReadChildBlock(lines, ref cursor, indent, diagnostics, task.Span, "TASK definition", out IReadOnlyList<SurfaceStatementSyntax>? children)) continue;
                if (Compilation.Tasks.SurfaceTaskHeader.TryParse(task, diagnostics, out string? name, out IReadOnlyList<string>? parameters, out string? resultType))
                    statements.Add(new SurfaceTaskDefinitionSyntax(name!, parameters!, resultType, children!, SourceSpan.FromBounds(task.Span.Start, children![^1].Span.End)));
                continue;
            }

            if (cursor < lines.Count && lines[cursor].Indent > indent) { diagnostics.Add(new SurfaceDiagnostic("FLN207", $"Statement '{DisplayName(parsed)}' cannot own an indented block.", parsed.Span)); int ignoredIndent = lines[cursor].Indent; _ = ParseBlock(lines, ref cursor, ignoredIndent, diagnostics); }
            statements.Add(parsed);
        }
        return statements;
    }

    private static bool TrySingleName(SurfaceCommandSyntax command, string owner, ICollection<SurfaceDiagnostic> diagnostics, out string? name)
    {
        name = null;
        if (command.Values.Count != 1 || command.Alias is not null) { diagnostics.Add(new SurfaceDiagnostic("FLN284", $"{owner} requires exactly one profile name.", command.Span)); return false; }
        string value = command.Values[0].UnquotedText.Trim();
        if (!Identifier(value)) { diagnostics.Add(new SurfaceDiagnostic("FLN284", $"Invalid policy profile name '{value}'.", command.Values[0].Span)); return false; }
        name = value; return true;
    }

    private static bool TryReadChildBlock(IReadOnlyList<LineInfo> lines, ref int cursor, int parentIndent, ICollection<SurfaceDiagnostic> diagnostics, SourceSpan ownerSpan, string owner, out IReadOnlyList<SurfaceStatementSyntax>? children)
    {
        children = null;
        if (cursor >= lines.Count || lines[cursor].Indent <= parentIndent) { diagnostics.Add(new SurfaceDiagnostic("FLN206", $"A {owner} must be followed by an indented block.", ownerSpan)); return false; }
        children = ParseBlock(lines, ref cursor, lines[cursor].Indent, diagnostics);
        return children.Count > 0;
    }

    private static SurfaceStatementSyntax? ParseLineStatement(LineInfo line, ICollection<SurfaceDiagnostic> diagnostics)
    {
        string trimmed = line.Text.Trim(); int absoluteStart = line.Start + line.LeadingCharacters;
        IReadOnlyList<(string Text, int Offset)> parts = SplitPipes(trimmed, diagnostics, absoluteStart);
        if (parts.Count == 0) return null;
        if (parts.Count == 1) return ParseCommand(parts[0].Text, absoluteStart + parts[0].Offset, diagnostics);
        List<SurfaceCommandSyntax> stages = [];
        foreach ((string text, int offset) in parts)
        {
            SurfaceCommandSyntax? stage = ParseCommand(text, absoluteStart + offset, diagnostics);
            if (stage is null) continue;
            if (stage.NormalizedName is "FROM" or "FOR" or "POLICY" or "WITH" or "TASK") { diagnostics.Add(new SurfaceDiagnostic("FLN208", $"{stage.NormalizedName} cannot appear as a pipeline stage.", stage.Span)); continue; }
            stages.Add(stage);
        }
        return stages.Count == 0 ? null : new SurfacePipelineSyntax(stages, new SourceSpan(absoluteStart, trimmed.Length));
    }

    private static SurfaceCommandSyntax? ParseCommand(string text, int absoluteStart, ICollection<SurfaceDiagnostic> diagnostics)
    {
        text = text.Trim(); int verbEnd = 0; while (verbEnd < text.Length && !char.IsWhiteSpace(text[verbEnd])) verbEnd++;
        string verb = text[..verbEnd];
        if (verb.Length == 0 || !verb.All(character => char.IsLetter(character) || character is '_' or '-')) { diagnostics.Add(new SurfaceDiagnostic("FLN200", $"Invalid surface command name '{verb}'.", new SourceSpan(absoluteStart, Math.Max(1, verb.Length)))); return null; }
        string tail = verbEnd < text.Length ? text[verbEnd..].Trim() : string.Empty; int tailOffset = tail.Length == 0 ? text.Length : text.IndexOf(tail, StringComparison.Ordinal);
        (string valuesSource, string? alias, int aliasOffset) = SplitAlias(tail);
        List<SurfaceValueSyntax> values = SplitValues(valuesSource, absoluteStart + tailOffset, diagnostics);
        if (alias is not null && string.IsNullOrWhiteSpace(alias)) diagnostics.Add(new SurfaceDiagnostic("FLN201", "AS must be followed by a non-empty alias.", new SourceSpan(absoluteStart + tailOffset + aliasOffset, 2)));
        return new SurfaceCommandSyntax(verb, values, string.IsNullOrWhiteSpace(alias) ? null : alias.Trim(), new SourceSpan(absoluteStart, text.Length));
    }

    private static IReadOnlyList<(string Text, int Offset)> SplitPipes(string source, ICollection<SurfaceDiagnostic> diagnostics, int absoluteStart)
    {
        List<(string Text, int Offset)> result = []; int segmentStart = 0, depth = 0; char? quote = null; bool escaped = false;
        for (int index = 0; index <= source.Length; index++)
        {
            bool atEnd = index == source.Length; char current = atEnd ? '\0' : source[index];
            if (!atEnd) { if (escaped) { escaped = false; continue; } if (quote is not null) { if (current == '\\') escaped = true; else if (current == quote) quote = null; continue; } if (current is '"' or '\'') { quote = current; continue; } if (current is '(' or '[' or '{') { depth++; continue; } if (current is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; } }
            if (!atEnd && (current != '|' || depth != 0)) continue;
            string segment = source[segmentStart..index]; int left = 0; while (left < segment.Length && char.IsWhiteSpace(segment[left])) left++; int right = segment.Length; while (right > left && char.IsWhiteSpace(segment[right - 1])) right--;
            if (left == right) diagnostics.Add(new SurfaceDiagnostic("FLN209", "A pipeline stage cannot be empty.", new SourceSpan(absoluteStart + segmentStart, Math.Max(1, segment.Length)))); else result.Add((segment[left..right], segmentStart + left)); segmentStart = index + 1;
        }
        return result;
    }

    private static (string Values, string? Alias, int AliasOffset) SplitAlias(string source)
    {
        if (string.IsNullOrEmpty(source)) return (string.Empty, null, -1); int depth = 0; char? quote = null; bool escaped = false;
        for (int index = 0; index < source.Length - 1; index++)
        {
            char current = source[index]; if (escaped) { escaped = false; continue; } if (quote is not null) { if (current == '\\') escaped = true; else if (current == quote) quote = null; continue; } if (current is '"' or '\'') { quote = current; continue; } if (current is '(' or '[' or '{') { depth++; continue; } if (current is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; } if (depth != 0) continue;
            bool starts = (index == 0 || char.IsWhiteSpace(source[index - 1])) && (source[index] is 'A' or 'a') && (source[index + 1] is 'S' or 's') && (index + 2 == source.Length || char.IsWhiteSpace(source[index + 2]));
            if (starts) return (source[..index].TrimEnd(), index + 2 < source.Length ? source[(index + 2)..].Trim() : string.Empty, index);
        }
        return (source, null, -1);
    }

    private static List<SurfaceValueSyntax> SplitValues(string source, int sourceStart, ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceValueSyntax> values = []; if (string.IsNullOrWhiteSpace(source)) return values; int segmentStart = 0, depth = 0; char? quote = null; bool escaped = false;
        for (int index = 0; index <= source.Length; index++)
        {
            bool atEnd = index == source.Length; char current = atEnd ? '\0' : source[index];
            if (!atEnd) { if (escaped) { escaped = false; continue; } if (quote is not null) { if (current == '\\') escaped = true; else if (current == quote) quote = null; continue; } if (current is '"' or '\'') { quote = current; continue; } if (current is '(' or '[' or '{') { depth++; continue; } if (current is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; } }
            if (!atEnd && (current != ',' || depth != 0)) continue;
            string segment = source[segmentStart..index]; int left = 0; while (left < segment.Length && char.IsWhiteSpace(segment[left])) left++; int right = segment.Length; while (right > left && char.IsWhiteSpace(segment[right - 1])) right--;
            if (left == right) diagnostics.Add(new SurfaceDiagnostic("FLN202", "A compact value cannot be empty.", new SourceSpan(sourceStart + segmentStart, Math.Max(1, segment.Length)))); else values.Add(new SurfaceValueSyntax(segment[left..right], new SourceSpan(sourceStart + segmentStart + left, right - left))); segmentStart = index + 1;
        }
        if (quote is not null || depth != 0) diagnostics.Add(new SurfaceDiagnostic("FLN203", "Unclosed quote or delimiter in compact statement.", new SourceSpan(sourceStart, source.Length)));
        return values;
    }

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
            if (current != ';' || depth != 0) continue;

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
        while (left < end && char.IsWhiteSpace(source[left])) left++;
        int right = end;
        while (right > left && char.IsWhiteSpace(source[right - 1])) right--;

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

    private static string DisplayName(SurfaceStatementSyntax statement) => statement switch
    {
        SurfaceCommandSyntax command => command.Name, SurfacePipelineSyntax => "pipeline", SurfaceContextSyntax => "context",
        SurfacePolicyDefinitionSyntax => "policy", SurfacePolicyContextSyntax => "policy context", SurfaceTaskDefinitionSyntax => "task", _ => statement.GetType().Name
    };
    private static bool Identifier(string value) => value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_') && value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
    private static IEnumerable<LineInfo> Lines(string source)
    {
        int start = 0; for (int index = 0; index <= source.Length; index++) { if (index < source.Length && source[index] != '\n') continue; int length = index - start; if (length > 0 && source[start + length - 1] == '\r') length--; string text = source.Substring(start, length); int charIndex = 0, indent = 0; while (charIndex < text.Length && text[charIndex] is ' ' or '\t') { indent += text[charIndex] == '\t' ? 4 : 1; charIndex++; } yield return new LineInfo(text, start, indent, charIndex); start = index + 1; }
    }
    private sealed record LineInfo(string Text, int Start, int Indent, int LeadingCharacters);
}
