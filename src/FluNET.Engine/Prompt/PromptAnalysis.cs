using FluNET.Execution.Planning;
using FluNET.Language.Binding;
using FluNET.Syntax.Validation;

namespace FluNET.Prompt;

/// <summary>
/// Side-effect-free analysis of a FluNET source document.
/// </summary>
public record PromptAnalysis(
    ProcessedPrompt Prompt,
    ValidationResult ValidationResult)
{
    /// <summary>
    /// True when binding, validation and planning produced a usable program.
    /// </summary>
    public bool IsValid => ValidationResult.IsValid && Plan is not null;

    public IReadOnlyList<PromptDiagnostic> Diagnostics => Prompt.Diagnostics;
    public IReadOnlyList<BoundCommand> BoundCommands { get; init; } = Array.Empty<BoundCommand>();
    public ExecutionPlan? Plan { get; init; }
}
