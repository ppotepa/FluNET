using FluNET.Automation;
using FluNET.Prompt;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Declarative.Reconciliation;

public sealed record ReconciliationWatchDefinition(
    string Id,
    WatchTriggerDefinition Trigger,
    IReadOnlyList<SyncDefinition> SyncDefinitions,
    SourceSpan Span);

public sealed record ReconciliationWatchDiagnostic(string Code, string Message, SourceSpan Span);

public sealed record ReconciliationWatchCompilationResult(
    IReadOnlyList<ReconciliationWatchDefinition> Watches,
    IReadOnlyList<ReconciliationWatchDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0 && Watches.All(watch => watch.SyncDefinitions.All(sync => sync.IsValid));
}

public sealed class ReconciliationWatchCompiler(SyncCompiler syncCompiler)
{
    public ReconciliationWatchCompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Line[] lines = ReadLines(source)
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && !line.Text.TrimStart().StartsWith('#'))
            .ToArray();
        List<ReconciliationWatchDefinition> watches = [];
        List<ReconciliationWatchDiagnostic> diagnostics = [];
        if (lines.Length == 0) return new([], []);
        int rootIndent = lines.Min(line => line.Indent);
        int cursor = 0;
        while (cursor < lines.Length)
        {
            Line header = lines[cursor++];
            string text = header.Text.Trim();
            if (header.Indent != rootIndent || !text.StartsWith("WATCH ", StringComparison.OrdinalIgnoreCase))
            { diagnostics.Add(new("FLN350", "Expected a root-level WATCH reconciliation trigger.", header.Span)); continue; }
            string resource = text[6..].Trim();
            if (resource.Length == 0) { diagnostics.Add(new("FLN351", "WATCH requires a resource name.", header.Span)); continue; }
            List<Line> children = [];
            while (cursor < lines.Length && lines[cursor].Indent > rootIndent) children.Add(lines[cursor++]);
            if (children.Count == 0) { diagnostics.Add(new("FLN352", "WATCH requires an indented SYNC body.", header.Span)); continue; }

            string? eventName = null;
            IReadOnlyList<Line> body = children;
            Line first = children[0];
            if (first.Text.TrimStart().StartsWith("WHEN ", StringComparison.OrdinalIgnoreCase))
            {
                eventName = first.Text.Trim()[5..].Trim();
                int whenIndent = first.Indent;
                body = children.Skip(1).Where(line => line.Indent > whenIndent).ToArray();
                if (eventName.Length == 0 || body.Count == 0)
                { diagnostics.Add(new("FLN353", "WHEN requires an event name and indented SYNC body.", first.Span)); continue; }
            }

            SyncCompilationResult sync = syncCompiler.Compile(Deindent(body));
            foreach (SyncDiagnostic item in sync.Diagnostics)
                diagnostics.Add(new("FLN354", item.Message, header.Span));
            if (!sync.IsValid) continue;
            watches.Add(new(Id(resource, eventName, watches.Count), new WatchTriggerDefinition(resource, eventName), sync.Definitions, header.Span));
        }
        return new(watches, diagnostics);
    }

    private static string Id(string resource, string? eventName, int index)
    {
        string input = $"{index}|{resource}|{eventName}";
        return "reconcile-watch-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant()[..12];
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

    private static IEnumerable<Line> ReadLines(string source)
    {
        int start = 0;
        for (int index = 0; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] != '\n') continue;
            int length = index - start;
            if (length > 0 && source[start + length - 1] == '\r') length--;
            string text = source.Substring(start, length);
            int chars = 0, indent = 0;
            while (chars < text.Length && text[chars] is ' ' or '\t')
            { indent += text[chars] == '\t' ? 4 : 1; chars++; }
            yield return new(text, start, indent, new SourceSpan(start, Math.Max(1, length)));
            start = index + 1;
        }
    }

    private sealed record Line(string Text, int Start, int Indent, SourceSpan Span);
}

public sealed record ReconciliationWatchRunResult(
    ReconciliationWatchDefinition Watch,
    IReadOnlyList<ReconciliationRunResult> Reconciliations)
{
    public bool IsSuccess => Reconciliations.All(result => result.IsSuccess);
}

/// <summary>Host-driven signal bridge. It owns no watcher thread or polling loop.</summary>
public sealed class ReconciliationWatchScheduler(ReconciliationRunner runner)
{
    private readonly Dictionary<string, ReconciliationWatchDefinition> watches = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ReconciliationWatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        watches[definition.Id] = definition;
    }

    public async ValueTask<IReadOnlyList<ReconciliationWatchRunResult>> PublishSignalAsync(
        string resource,
        string? eventName = null,
        CancellationToken cancellationToken = default)
    {
        List<ReconciliationWatchRunResult> runs = [];
        foreach (ReconciliationWatchDefinition watch in watches.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (!watch.Trigger.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) ||
                !(watch.Trigger.Event is null || watch.Trigger.Event.Equals(eventName, StringComparison.OrdinalIgnoreCase))) continue;
            List<ReconciliationRunResult> reconciliations = [];
            foreach (SyncDefinition sync in watch.SyncDefinitions)
                reconciliations.Add(await runner.RunAsync(sync, null, cancellationToken).ConfigureAwait(false));
            runs.Add(new(watch, reconciliations));
        }
        return runs;
    }
}
