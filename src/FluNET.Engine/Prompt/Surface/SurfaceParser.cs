namespace FluNET.Prompt.Surface;

public sealed partial class SurfaceParser
{
    public SurfaceParseResult Parse(SourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<SurfaceDiagnostic> diagnostics = [];
        LineInfo[] lines = Lines(document.Text)
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && !line.Text.TrimStart().StartsWith('#'))
            .SelectMany(line => SplitStatements(line, diagnostics))
            .Select(line => line with { SentenceIndex = FindSentenceIndex(document.Sentences, line.Start) })
            .ToArray();
        int cursor = 0;
        int rootIndent = lines.Length == 0 ? 0 : lines.Min(line => line.Indent);
        IReadOnlyList<SurfaceStatementSyntax> statements = ParseBlock(
            lines,
            ref cursor,
            rootIndent,
            diagnostics);
        SourceSpan span = statements.Count == 0
            ? default
            : SourceSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new SurfaceParseResult(
            document,
            new SurfaceProgramSyntax(statements, span),
            diagnostics);
    }

    private static IReadOnlyList<SurfaceStatementSyntax> ParseBlock(
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int indent,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> statements = [];
        while (cursor < lines.Count)
        {
            LineInfo line = lines[cursor];
            if (line.Indent < indent)
                break;
            if (line.Indent > indent)
            {
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN204",
                    "Unexpected indentation. Only block-form surface statements may introduce an indented block.",
                    new SourceSpan(line.Start, Math.Max(1, line.LeadingCharacters))));
                cursor++;
                continue;
            }

            SurfaceStatementSyntax? parsed = ParseLineStatement(line, diagnostics);
            if (parsed is not null && line.SentenceIndex >= 0)
                parsed = parsed with { SentenceIndex = line.SentenceIndex };
            cursor++;
            if (parsed is null)
                continue;

            if (TryParseBlockStatement(
                    parsed,
                    line,
                    lines,
                    ref cursor,
                    indent,
                    diagnostics,
                    statements))
            {
                continue;
            }

            if (cursor < lines.Count && lines[cursor].Indent > indent)
            {
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN207",
                    $"Statement '{DisplayName(parsed)}' cannot own an indented block.",
                    parsed.Span));
                int ignoredIndent = lines[cursor].Indent;
                _ = ParseBlock(lines, ref cursor, ignoredIndent, diagnostics);
            }

            statements.Add(parsed);
        }

        return statements;
    }

    private static bool TryReadChildBlock(
        IReadOnlyList<LineInfo> lines,
        ref int cursor,
        int parentIndent,
        ICollection<SurfaceDiagnostic> diagnostics,
        SourceSpan ownerSpan,
        string owner,
        out IReadOnlyList<SurfaceStatementSyntax>? children)
    {
        children = null;
        if (cursor >= lines.Count || lines[cursor].Indent <= parentIndent)
        {
            diagnostics.Add(new SurfaceDiagnostic(
                "FLN206",
                $"A {owner} must be followed by an indented block.",
                ownerSpan));
            return false;
        }

        children = ParseBlock(lines, ref cursor, lines[cursor].Indent, diagnostics);
        return children.Count > 0;
    }
}
