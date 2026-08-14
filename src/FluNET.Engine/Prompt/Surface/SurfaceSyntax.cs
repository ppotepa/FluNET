namespace FluNET.Prompt.Surface;

public sealed record SurfaceProgramSyntax(
    IReadOnlyList<SurfaceStatementSyntax> Statements,
    SourceSpan Span);

public abstract record SurfaceStatementSyntax(SourceSpan Span);

/// <summary>A compact/canonical command before inference and lowering.</summary>
public sealed record SurfaceCommandSyntax(
    string Name,
    IReadOnlyList<SurfaceValueSyntax> Values,
    string? Alias,
    SourceSpan Span) : SurfaceStatementSyntax(Span)
{
    public string NormalizedName => Name.ToUpperInvariant();
}

/// <summary>One source-level value. Its meaning is deliberately unresolved here.</summary>
public sealed record SurfaceValueSyntax(string Text, SourceSpan Span)
{
    public string UnquotedText =>
        Text.Length >= 2 &&
        ((Text[0] == '"' && Text[^1] == '"') || (Text[0] == '\'' && Text[^1] == '\''))
            ? Text[1..^1]
            : Text;
}
