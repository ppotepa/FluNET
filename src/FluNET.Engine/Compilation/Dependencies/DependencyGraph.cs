using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using System.Collections.ObjectModel;

namespace FluNET.Compilation.Dependencies;

public enum DependencyKind
{
    Data,
    Control,
    Condition,
    Effect,
    Scope
}

public sealed record DependencyNode(
    int Index,
    BoundCommand Command,
    FrameExecutionMetadata Metadata);

public sealed record DependencyEdge(
    int From,
    int To,
    DependencyKind Kind,
    string? Variable = null);

public sealed class DependencyGraph
{
    private readonly ReadOnlyCollection<DependencyNode> _nodes;
    private readonly ReadOnlyCollection<DependencyEdge> _edges;

    public DependencyGraph(
        BoundProgram program,
        PromptSyntax syntax,
        IEnumerable<DependencyNode> nodes,
        IEnumerable<DependencyEdge> edges)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        _nodes = Array.AsReadOnly(nodes?.OrderBy(node => node.Index).ToArray()
            ?? throw new ArgumentNullException(nameof(nodes)));
        _edges = Array.AsReadOnly(edges?.Distinct().OrderBy(edge => edge.To).ThenBy(edge => edge.From).ToArray()
            ?? throw new ArgumentNullException(nameof(edges)));
    }

    public BoundProgram Program { get; }
    public PromptSyntax Syntax { get; }
    public IReadOnlyList<DependencyNode> Nodes => _nodes;
    public IReadOnlyList<DependencyEdge> Edges => _edges;

    public IEnumerable<DependencyEdge> Incoming(int index) => _edges.Where(edge => edge.To == index);
}
