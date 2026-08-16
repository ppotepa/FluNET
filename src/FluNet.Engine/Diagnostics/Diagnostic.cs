namespace FluNET.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// Language-level diagnostic intended for parser, binder and runtime tooling.
/// Codes are stable API and use the FLU prefix.
/// </summary>
public sealed record Diagnostic(
    string Code,
    string Message,
    DiagnosticSeverity Severity,
    TextSpan? Span = null,
    string? Suggestion = null)
{
    public static Diagnostic Error(string code, string message, TextSpan? span = null, string? suggestion = null) =>
        new(code, message, DiagnosticSeverity.Error, span, suggestion);
}
