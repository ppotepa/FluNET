using System.Collections.ObjectModel;
using FluNET.Prompt;

namespace FluNET.Compilation.Inference;

public enum InferenceKind
{
    Resource,
    Format,
    Type,
    VariableName,
    Dependency,
    Scheduling,
    Context
}

public enum InferenceConfidence
{
    Certain,
    Explicit
}

public sealed record InferenceDecision(
    InferenceKind Kind,
    string Input,
    string Result,
    string Rule,
    SourceSpan Span,
    InferenceConfidence Confidence = InferenceConfidence.Certain);

/// <summary>Append-only explanation of every automatic surface-language decision.</summary>
public sealed class InferenceTrace
{
    private readonly List<InferenceDecision> _items = [];

    public IReadOnlyList<InferenceDecision> Items => new ReadOnlyCollection<InferenceDecision>(_items);

    public void Add(InferenceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        _items.Add(decision);
    }
}
