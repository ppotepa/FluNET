using FluNET.Prompt;

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
        Sentences = SentenceSegmenter.Segment(text);
    }

    public string Text { get; }
    public SourceSyntaxKind SyntaxKind { get; }
    public string? Path { get; }
    public IReadOnlyList<Sentence> Sentences { get; }

    /// <summary>Resolves a parsed/lowered span back to its source sentence.</summary>
    public Sentence? FindSentence(SourceSpan span) =>
        Sentences.FirstOrDefault(sentence =>
            sentence.Span.Start <= span.Start && sentence.Span.End >= span.Start);
}

/// <summary>Parser result for a source document. Surface parsing never performs I/O or inference.</summary>
public sealed record SurfaceParseResult(
    SourceDocument Document,
    SurfaceProgramSyntax Program,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
    public IReadOnlyList<Sentence> Sentences => Document.Sentences;
}

public sealed record SurfaceDiagnostic(string Code, string Message, SourceSpan Span);
