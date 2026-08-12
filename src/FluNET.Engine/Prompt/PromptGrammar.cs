using System.Collections.ObjectModel;

namespace FluNET.Prompt;

/// <summary>How a surface connector relates two commands.</summary>
public enum CommandLinkKind
{
    Sequence,
    Parallel,
    Alternative
}

public enum CommandModifierKind
{
    Retry,
    Timeout,
    ErrorPolicy,
    Condition
}

public sealed record CommandModifierDescriptor(
    string Introducer,
    string? Name,
    CommandModifierKind Kind);

/// <summary>An immutable grammar projection used by the prompt parser.</summary>
public sealed class PromptGrammar
{
    private readonly IReadOnlyDictionary<string, PromptClauseKind> _clauseMarkers;
    private readonly IReadOnlyDictionary<string, CommandLinkKind> _commandConnectors;
    private readonly IReadOnlyList<CommandModifierDescriptor> _commandModifiers;

    public PromptGrammar(
        IEnumerable<KeyValuePair<string, PromptClauseKind>> clauseMarkers,
        IEnumerable<KeyValuePair<string, CommandLinkKind>> commandConnectors,
        IEnumerable<CommandModifierDescriptor>? commandModifiers = null)
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
        _commandModifiers = (commandModifiers ?? Array.Empty<CommandModifierDescriptor>())
            .Select(descriptor => new CommandModifierDescriptor(
                Normalize(descriptor.Introducer),
                string.IsNullOrWhiteSpace(descriptor.Name) ? null : Normalize(descriptor.Name),
                descriptor.Kind))
            .ToArray();
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
            ["THEN"] = CommandLinkKind.Sequence,
            ["AND"] = CommandLinkKind.Parallel,
            ["ELSE"] = CommandLinkKind.Alternative
        },
        new[]
        {
            new CommandModifierDescriptor("WITH", "RETRY", CommandModifierKind.Retry),
            new CommandModifierDescriptor("WITH", "TIMEOUT", CommandModifierKind.Timeout),
            new CommandModifierDescriptor("ON", "ERROR", CommandModifierKind.ErrorPolicy),
            new CommandModifierDescriptor("IF", null, CommandModifierKind.Condition)
        });

    public IEnumerable<string> ClauseMarkers => _clauseMarkers.Keys;
    public IEnumerable<string> CommandConnectors => _commandConnectors.Keys;
    public IReadOnlyList<CommandModifierDescriptor> CommandModifiers => _commandModifiers;

    public bool TryGetClauseKind(string surface, out PromptClauseKind kind) =>
        _clauseMarkers.TryGetValue(surface, out kind);

    public bool TryGetLinkKind(string surface, out CommandLinkKind kind) =>
        _commandConnectors.TryGetValue(surface, out kind);

    public bool TryGetModifier(
        IReadOnlyList<PromptToken> tokens,
        int index,
        out CommandModifierDescriptor? modifier,
        out int consumed)
    {
        modifier = _commandModifiers.FirstOrDefault(candidate =>
            index < tokens.Count &&
            tokens[index].Text.Equals(candidate.Introducer, StringComparison.OrdinalIgnoreCase) &&
            (candidate.Name is null ||
             index + 1 < tokens.Count &&
             tokens[index + 1].Text.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase)));
        consumed = modifier is null ? 0 : modifier.Name is null ? 1 : 2;
        return modifier is not null;
    }

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A grammar surface form cannot be empty.", nameof(value))
            : value.Trim().ToUpperInvariant();
}
