using FluNET.Sentences;
using FluNET.Syntax.Validation;
<<<<<<< HEAD
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
=======
>>>>>>> origin/agent/stabilize-poc-foundation

namespace FluNET.Execution;

public enum ExecutionFailureKind
{
    None,
    Syntax,
<<<<<<< HEAD
    Binding,
=======
>>>>>>> origin/agent/stabilize-poc-foundation
    Validation,
    Activation,
    Capability,
    Execution,
    Cancelled,
    Internal
}

public sealed record ExecutionError(
    ExecutionFailureKind Kind,
    string Code,
    string Message,
    Exception? Exception = null);

/// <summary>A structured result that never disguises an execution failure as null.</summary>
public sealed class ExecutionResult
{
    public ValidationResult ValidationResult { get; }
    public ISentence? Sentence { get; }
    public object? Result { get; }
    public ExecutionError? Error { get; }
<<<<<<< HEAD
    public ExecutionPlan? Plan { get; }
    public IReadOnlyList<ExecutionStepResult> Steps { get; }
    public WorkflowRunState? Workflow { get; }
=======
>>>>>>> origin/agent/stabilize-poc-foundation
    public bool IsSuccess => Error is null && ValidationResult.IsValid;

    private ExecutionResult(
        ValidationResult validationResult,
        ISentence? sentence,
        object? result,
<<<<<<< HEAD
        ExecutionError? error,
        ExecutionPlan? plan,
        IEnumerable<ExecutionStepResult>? steps,
        WorkflowRunState? workflow = null)
=======
        ExecutionError? error)
>>>>>>> origin/agent/stabilize-poc-foundation
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        Sentence = sentence;
        Result = result;
        Error = error;
<<<<<<< HEAD
        Plan = plan;
        Steps = steps?.ToArray() ?? Array.Empty<ExecutionStepResult>();
        Workflow = workflow;
    }

    /// <summary>
    /// Creates a successful canonical execution result. Sentence is optional and
    /// is populated only by compatibility/custom pipelines.
    /// </summary>
    public static ExecutionResult Success(
        ISentence? sentence,
        object? result,
        ExecutionPlan? plan = null,
        IEnumerable<ExecutionStepResult>? steps = null,
        WorkflowRunState? workflow = null) =>
        new(ValidationResult.Success(), sentence, result, null, plan, steps, workflow);
=======
    }

    public static ExecutionResult Success(ISentence sentence, object? result) =>
        new(ValidationResult.Success(), sentence, result, null);
>>>>>>> origin/agent/stabilize-poc-foundation

    public static ExecutionResult Failed(ValidationResult validationResult) =>
        new(
            validationResult,
            null,
            null,
            new ExecutionError(
                ExecutionFailureKind.Validation,
                "FLN100",
<<<<<<< HEAD
                validationResult.FailureReason ?? "Validation failed."),
            null,
            null,
            null);
=======
                validationResult.FailureReason ?? "Validation failed."));
>>>>>>> origin/agent/stabilize-poc-foundation

    public static ExecutionResult Failed(
        ExecutionFailureKind kind,
        string code,
        string message,
        Exception? exception = null,
<<<<<<< HEAD
        ISentence? sentence = null,
        ExecutionPlan? plan = null,
        IEnumerable<ExecutionStepResult>? steps = null,
        WorkflowRunState? workflow = null) =>
=======
        ISentence? sentence = null) =>
>>>>>>> origin/agent/stabilize-poc-foundation
        new(
            kind == ExecutionFailureKind.Validation
                ? ValidationResult.Failure(message)
                : ValidationResult.Success(),
            sentence,
            null,
<<<<<<< HEAD
            new ExecutionError(kind, code, message, exception),
            plan,
            steps,
            workflow);
=======
            new ExecutionError(kind, code, message, exception));
>>>>>>> origin/agent/stabilize-poc-foundation

    public static ExecutionResult Failed(string message) =>
        Failed(ExecutionFailureKind.Internal, "FLN999", message);
}
