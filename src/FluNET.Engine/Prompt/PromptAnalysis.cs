<<<<<<< HEAD
using FluNET.Execution.Planning;
using FluNET.Language.Binding;
=======
>>>>>>> origin/agent/stabilize-poc-foundation
using FluNET.Sentences;
using FluNET.Syntax.Validation;

namespace FluNET.Prompt;

<<<<<<< HEAD
/// <summary>
/// Compatibility view of a side-effect-free analysis. The concrete value
/// returned by Engine.Analyze is CompilationResult and carries the canonical
/// parsed/bound program and compiler diagnostics in addition to these members.
/// </summary>
public record PromptAnalysis(
=======
/// <summary>The side-effect-free result of parsing and validating a prompt.</summary>
public sealed record PromptAnalysis(
>>>>>>> origin/agent/stabilize-poc-foundation
    ProcessedPrompt Prompt,
    ValidationResult ValidationResult,
    ISentence? Sentence)
{
<<<<<<< HEAD
    /// <summary>
    /// True for a valid canonical plan. A legacy ISentence is optional because
    /// native typed modules intentionally have no sentence representation.
    /// </summary>
    public bool IsValid => ValidationResult.IsValid && (Plan is not null || Sentence is not null);

    public IReadOnlyList<PromptDiagnostic> Diagnostics => Prompt.Diagnostics;
    public IReadOnlyList<BoundCommand> BoundCommands { get; init; } = Array.Empty<BoundCommand>();
    public ExecutionPlan? Plan { get; init; }
=======
    public bool IsValid => ValidationResult.IsValid && Sentence is not null;
    public IReadOnlyList<PromptDiagnostic> Diagnostics => Prompt.Diagnostics;
>>>>>>> origin/agent/stabilize-poc-foundation
}
