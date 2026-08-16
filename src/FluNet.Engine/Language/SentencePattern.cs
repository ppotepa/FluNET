using FluNET.Syntax.Core;

namespace FluNET.Language;

/// <summary>
/// Semantic roles that make up a FluNET sentence.
/// </summary>
public enum ClauseKind
{
    What,
    From,
    To,
    Using,
    With,
    Then
}

public enum RoleCardinality
{
    One,
    ZeroOrOne,
    OneOrMore,
    ZeroOrMore
}

public sealed record ClauseDescriptor(
    ClauseKind Kind,
    Type ValueType,
    bool Required = true,
    string? Name = null,
    RoleDirection Direction = RoleDirection.Input,
    RoleCardinality Cardinality = RoleCardinality.One,
    Type? ElementType = null);

/// <summary>
/// Declarative grammar of a single verb sentence. Patterns are compiled from
/// interfaces, CLR types, constructor signatures and optional attribute overrides.
/// </summary>
public sealed record SentencePattern(
    string Verb,
    IReadOnlyList<ClauseDescriptor> Clauses)
{
    public bool Has(ClauseKind kind) => Clauses.Any(x => x.Kind == kind);

    public ClauseDescriptor? Find(ClauseKind kind) =>
        Clauses.FirstOrDefault(x => x.Kind == kind);

    public IReadOnlyList<ClauseDescriptor> FindAll(ClauseKind kind) =>
        Clauses.Where(x => x.Kind == kind).ToArray();
}
