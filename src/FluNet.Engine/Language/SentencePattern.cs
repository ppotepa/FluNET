namespace FluNET.Language;

/// <summary>
/// Semantic roles that make up a FluNET sentence. They intentionally mirror
/// the English-like surface syntax and are independent from the parser/runtime.
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

public sealed record ClauseDescriptor(
    ClauseKind Kind,
    Type ValueType,
    bool Required = true);

/// <summary>
/// Declarative grammar of a single verb sentence, e.g. GET WHAT&lt;string&gt; FROM&lt;FileInfo&gt;.
/// The same model can be consumed by parsing, validation, tooling and documentation.
/// </summary>
public sealed record SentencePattern(
    string Verb,
    IReadOnlyList<ClauseDescriptor> Clauses)
{
    public bool Has(ClauseKind kind) => Clauses.Any(x => x.Kind == kind);

    public ClauseDescriptor? Find(ClauseKind kind) =>
        Clauses.FirstOrDefault(x => x.Kind == kind);
}
