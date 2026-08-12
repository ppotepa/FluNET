using System.Collections.ObjectModel;

namespace FluNET.Prompt;

/// <summary>How a surface connector relates two commands.</summary>
public enum CommandLinkKind
{
    Sequence,
    Parallel
}

/// <summary>An immutable grammar projection used by the prompt parser.</summary>
public sealed class PromptGrammar
{
    private readonly IReadOnlyDictionary<string, PromptClauseKind> _clauseMarkers;
    private readonly IReadOnlyDictionary<string, CommandLinkKind> _commandConnectors;

    public PromptGrammar(
        IEnumerable<KeyValuePair<string, PromptClauseKind>> clauseMarkers,
        IEnumerable<KeyValuePair<string, CommandLinkKind>> commandConnectors)
    {
        ArgumentNullException.ThrowIfNull(clauseMarkers);
        ArgumentNullException.ThrowIfNull(commandConnectors);
        _clauseMarkers = new ReadOnlyDictionary<string, PromptClauseKind>(
            clauseMarkers.ToDictionary(
                pair => Normalize(pair.Key),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase));
        _commandConnectors = new ReadOnlyDictionary<string, CommandLinkKind>(
            commandConnectors.ToDictionary(
                pair => Normalize(pair.Key),
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase));
    }

    public static PromptGrammar Standard { get; } = new(
        new Dictionary<string, PromptClauseKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["FROM"] = PromptClauseKind.From,
            ["TO"] = PromptClauseKind.To,
            ["USING"] = PromptClauseKind.Using
        },
        new Dictionary<string, CommandLinkKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["THEN"] = CommandLinkKind.Sequence
        });

    public IEnumerable<string> ClauseMarkers => _clauseMarkers.Keys;
    public IEnumerable<string> CommandConnectors => _commandConnectors.Keys;

    public bool TryGetClauseKind(string surface, out PromptClauseKind kind) =>
        _clauseMarkers.TryGetValue(surface, out kind);

    public bool TryGetLinkKind(string surface, out CommandLinkKind kind) =>
        _commandConnectors.TryGetValue(surface, out kind);

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A grammar surface form cannot be empty.", nameof(value))
            : value.Trim().ToUpperInvariant();
}
