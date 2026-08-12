namespace FluNET.Prompt;

public sealed class PromptSyntaxException : Exception
{
    public PromptSyntaxException(string message, IReadOnlyList<PromptDiagnostic> diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<PromptDiagnostic> Diagnostics { get; }
}
