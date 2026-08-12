using FluNET.Sentences;
using FluNET.Syntax.Validation;

namespace FluNET.Prompt;

/// <summary>The side-effect-free result of parsing and validating a prompt.</summary>
public sealed record PromptAnalysis(
    ProcessedPrompt Prompt,
    ValidationResult ValidationResult,
    ISentence? Sentence)
{
    public bool IsValid => ValidationResult.IsValid && Sentence is not null;
    public IReadOnlyList<PromptDiagnostic> Diagnostics => Prompt.Diagnostics;
}
