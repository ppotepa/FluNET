using System.Collections.ObjectModel;
using FluNET.Prompt;

namespace FluNET.Compilation.Lowering;

public sealed record SourceMapEntry(
    int CommandIndex,
    string NodeKind,
    SourceSpan SourceSpan);

/// <summary>Maps canonical nodes back to the user's original surface source.</summary>
public sealed class SourceMap
{
    private readonly ReadOnlyCollection<SourceMapEntry> _entries;

    public SourceMap(IEnumerable<SourceMapEntry>? entries = null)
    {
        _entries = Array.AsReadOnly(entries?.ToArray() ?? []);
    }

    public IReadOnlyList<SourceMapEntry> Entries => _entries;

    public SourceSpan? FindCommand(int commandIndex) =>
        _entries.FirstOrDefault(entry =>
            entry.CommandIndex == commandIndex && entry.NodeKind == "command")?.SourceSpan;
}
