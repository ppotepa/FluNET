namespace FluNET.Prompt.Surface;

public sealed record SurfaceProgramSyntax(IReadOnlyList<SurfaceStatementSyntax> Statements, SourceSpan Span);
public abstract record SurfaceStatementSyntax(SourceSpan Span)
{
    /// <summary>Index of the source Sentence which produced this statement.</summary>
    public int SentenceIndex { get; init; } = -1;
}
public sealed record SurfaceCommandSyntax(string Name, IReadOnlyList<SurfaceValueSyntax> Values, string? Alias, SourceSpan Span) : SurfaceStatementSyntax(Span)
{
    public string NormalizedName => Name.ToUpperInvariant();
}
public sealed record SurfacePipelineSyntax(IReadOnlyList<SurfaceCommandSyntax> Stages, SourceSpan Span) : SurfaceStatementSyntax(Span);
public sealed record SurfaceContextSyntax(SurfaceValueSyntax BaseResource, IReadOnlyList<SurfaceStatementSyntax> Statements, SourceSpan Span) : SurfaceStatementSyntax(Span);
public sealed record SurfacePolicyDefinitionSyntax(string Name, IReadOnlyList<SurfaceStatementSyntax> Statements, SourceSpan Span) : SurfaceStatementSyntax(Span);
public sealed record SurfacePolicyContextSyntax(string Name, IReadOnlyList<SurfaceStatementSyntax> Statements, SourceSpan Span) : SurfaceStatementSyntax(Span);
public sealed record SurfaceTaskDefinitionSyntax(
    string Name,
    IReadOnlyList<string> Parameters,
    string? ResultTypeName,
    IReadOnlyList<SurfaceStatementSyntax> Statements,
    SourceSpan Span) : SurfaceStatementSyntax(Span);

public sealed record SurfaceRepeatSyntax(
    int Count,
    IReadOnlyList<SurfaceStatementSyntax> Statements,
    SourceSpan Span) : SurfaceStatementSyntax(Span);

public sealed record SurfaceWhileSyntax(
    SurfaceWhileDescriptor Descriptor,
    SourceSpan Span) : SurfaceStatementSyntax(Span);

public sealed record SurfaceIfSyntax(
    string Condition,
    IReadOnlyList<SurfaceStatementSyntax> WhenTrue,
    IReadOnlyList<SurfaceStatementSyntax> WhenFalse,
    SourceSpan Span) : SurfaceStatementSyntax(Span);

public sealed record SurfaceValueSyntax(string Text, SourceSpan Span)
{
    public string UnquotedText => Text.Length >= 2 &&
        ((Text[0] == '"' && Text[^1] == '"') || (Text[0] == '\'' && Text[^1] == '\''))
            ? Text[1..^1]
            : Text;
}
