using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Language.Binding;
using FluNET.Execution.Planning;

namespace FluNET.Prompt;

/// <summary>
/// Compatibility view of a side-effect-free analysis. The concrete value
/// returned by Engine.Analyze is CompilationResult and carries the canonical
/// parsed/bound program and compiler diagnostics in addition to these members.
/// </summary>
public record PromptAnalysis(
    ProcessedPrompt Prompt,
    ValidationResult ValidationResult,
    ISentence? Sentence)
{
    public bool IsValid => ValidationResult.IsValid && Sentence is not null;
    public IReadOnlyList<PromptDiagnostic> Diagnostics => Prompt.Diagnostics;
    public IReadOnlyList<BoundCommand> BoundCommands { get; init; } = Array.Empty<BoundCommand>();
    public ExecutionPlan? Plan { get; init; }
}
