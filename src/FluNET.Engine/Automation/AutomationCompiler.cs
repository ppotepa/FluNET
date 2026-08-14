using FluNET.Compilation;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Automation;

/// <summary>Compiles EVERY/WATCH/WHEN source blocks into trigger metadata plus normal execution plans.</summary>
public sealed class AutomationCompiler(SurfaceCompiler surfaceCompiler)
{
    public AutomationCompilationResult Compile(SourceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<AutomationDefinition> automations = [];
        List<AutomationDiagnostic> diagnostics = [];
        Line[] lines = ReadLines(document.Text).Where(line => !string.IsNullOrWhiteSpace(line.Text) && !line.Text.TrimStart().StartsWith('#')).ToArray();
        if (lines.Length == 0) return new([], []);
        int rootIndent = lines.Min(line => line.Indent);
        int cursor = 0, index = 0;
        while (cursor < lines.Length)
        {
            Line header = lines[cursor];
            if (header.Indent != rootIndent)
            {
                diagnostics.Add(new("FLN310", "Automation trigger must start at the root indentation level.", header.Span)); cursor++; continue;
            }
            string source = header.Text.Trim();
            cursor++;
            List<Line> children = [];
            while (cursor < lines.Length && lines[cursor].Indent > rootIndent) children.Add(lines[cursor++]);
            if (children.Count == 0)
            {
                diagnostics.Add(new("FLN311", "Automation trigger requires an indented workflow body.", header.Span)); continue;
            }

            TriggerDefinition? trigger = null;
            IReadOnlyList<Line> body = children;
            if (source.StartsWith("EVERY ", StringComparison.OrdinalIgnoreCase))
            {
                string duration = source[6..].Trim();
                if (!TryDuration(duration, out TimeSpan interval)) diagnostics.Add(new("FLN312", $"Invalid EVERY interval '{duration}'.", header.Span));
                else trigger = new IntervalTriggerDefinition(interval);
            }
            else if (source.StartsWith("WATCH ", StringComparison.OrdinalIgnoreCase))
            {
                string resource = source[6..].Trim();
                if (resource.Length == 0) diagnostics.Add(new("FLN313", "WATCH requires a resource expression.", header.Span));
                else
                {
                    string? eventName = null;
                    Line first = children[0];
                    if (first.Text.TrimStart().StartsWith("WHEN ", StringComparison.OrdinalIgnoreCase))
                    {
                        eventName = first.Text.Trim()[5..].Trim();
                        int whenIndent = first.Indent;
                        Line[] eventBody = children.Skip(1).Where(line => line.Indent > whenIndent).ToArray();
                        if (eventName.Length == 0 || eventBody.Length == 0)
                            diagnostics.Add(new("FLN314", "WHEN requires an event name and indented workflow body.", first.Span));
                        else body = eventBody;
                    }
                    trigger = new WatchTriggerDefinition(resource, eventName);
                }
            }
            else
            {
                diagnostics.Add(new("FLN315", "Expected EVERY or WATCH automation trigger.", header.Span));
            }
            if (trigger is null) continue;

            string bodySource = Deindent(body);
            SurfaceCompilationResult compilation = surfaceCompiler.Compile(new SourceDocument(bodySource, SourceSyntaxKind.Compact));
            if (!compilation.IsValid)
                diagnostics.Add(new("FLN316", "Automation workflow body does not compile.", header.Span));
            automations.Add(new AutomationDefinition(AutomationId.Create(source, index++), trigger, new WorkflowTemplate(compilation), header.Span));
        }
        return new(automations, diagnostics);
    }

    private static string Deindent(IReadOnlyList<Line> lines)
    {
        int indent = lines.Min(line => line.Indent);
        return string.Join(Environment.NewLine, lines.Select(line => RemoveIndent(line.Text, indent)));
    }

    private static string RemoveIndent(string text, int width)
    {
        int chars = 0, consumed = 0;
        while (chars < text.Length && consumed < width && text[chars] is ' ' or '\t')
        { consumed += text[chars] == '\t' ? 4 : 1; chars++; }
        return text[chars..];
    }

    private static bool TryDuration(string source, out TimeSpan interval)
    {
        string text = source.Trim().ToLowerInvariant();
        (string Number, double Seconds) part = text switch
        {
            _ when text.EndsWith("ms") => (text[..^2], .001),
            _ when text.EndsWith('s') => (text[..^1], 1),
            _ when text.EndsWith('m') => (text[..^1], 60),
            _ when text.EndsWith('h') => (text[..^1], 3600),
            _ when text.EndsWith('d') => (text[..^1], 86400),
            _ => (string.Empty, 0)
        };
        if (part.Number.Length == 0 || !double.TryParse(part.Number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number) || number <= 0)
        { interval = default; return false; }
        interval = TimeSpan.FromSeconds(number * part.Seconds);
        return interval > TimeSpan.Zero && interval <= TimeSpan.FromDays(365);
    }

    private static IEnumerable<Line> ReadLines(string source)
    {
        int start = 0;
        for (int index = 0; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] != '\n') continue;
            int length = index - start; if (length > 0 && source[start + length - 1] == '\r') length--;
            string text = source.Substring(start, length); int chars = 0, indent = 0;
            while (chars < text.Length && text[chars] is ' ' or '\t') { indent += text[chars] == '\t' ? 4 : 1; chars++; }
            yield return new Line(text, start, indent, new SourceSpan(start, Math.Max(1, length)));
            start = index + 1;
        }
    }
    private sealed record Line(string Text, int Start, int Indent, SourceSpan Span);
}
