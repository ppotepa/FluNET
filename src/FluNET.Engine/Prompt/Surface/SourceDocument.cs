namespace FluNET.Prompt.Surface;

/// <summary>How a source document should be interpreted before semantic binding.</summary>
public enum SourceSyntaxKind
{
    Auto,
    Canonical,
    Compact
}

/// <summary>Immutable source text plus the requested surface syntax mode.</summary>
public sealed record SourceDocument
{
    public SourceDocument(string text, SourceSyntaxKind syntaxKind = SourceSyntaxKind.Auto, string? path = null)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        SyntaxKind = syntaxKind;
        Path = string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public string Text { get; }
    public SourceSyntaxKind SyntaxKind { get; }
    public string? Path { get; }
}

/// <summary>Parser result for a source document. Surface parsing never performs I/O or inference.</summary>
public sealed record SurfaceParseResult(
    SourceDocument Document,
    SurfaceProgramSyntax Program,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed record SurfaceDiagnostic(string Code, string Message, SourceSpan Span);
