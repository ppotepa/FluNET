using FluNET.Prompt;

namespace FluNET.Execution.Commands;

public static class ExpressionDiagnosticCodes
{
    public const string ValueParseFailure = "FLN140";
    public const string ConversionNotFound = "FLN141";
    public const string ConversionAmbiguous = "FLN142";
    public const string ShapeMismatch = "FLN143";
}

public sealed class ExpressionBindingException(
    string code,
    string message,
    SourceSpan span,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public SourceSpan Span { get; } = span;
}
