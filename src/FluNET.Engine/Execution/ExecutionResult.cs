using FluNET.Syntax.Validation;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;

namespace FluNET.Execution;

public enum ExecutionFailureKind
{
    None,
    Syntax,
    Binding,
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
    public object? Result { get; }
    public ExecutionError? Error { get; }
    public ExecutionPlan? Plan { get; }
    public IReadOnlyList<ExecutionStepResult> Steps { get; }
    public WorkflowRunState? Workflow { get; }
    public bool IsSuccess => Error is null && ValidationResult.IsValid;

    private ExecutionResult(
        ValidationResult validationResult,
        object? result,
        ExecutionError? error,
        ExecutionPlan? plan,
        IEnumerable<ExecutionStepResult>? steps,
        WorkflowRunState? workflow = null)
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        Result = result;
        Error = error;
        Plan = plan;
        Steps = steps?.ToArray() ?? Array.Empty<ExecutionStepResult>();
        Workflow = workflow;
    }

    /// <summary>
    /// Creates a successful typed execution result.
    /// </summary>
    public static ExecutionResult Success(
        object? result,
        ExecutionPlan? plan = null,
        IEnumerable<ExecutionStepResult>? steps = null,
        WorkflowRunState? workflow = null) =>
        new(ValidationResult.Success(), result, null, plan, steps, workflow);

    public static ExecutionResult Failed(ValidationResult validationResult) =>
        new(
            validationResult,
            null,
            new ExecutionError(
                ExecutionFailureKind.Validation,
                "FLN100",
                validationResult.FailureReason ?? "Validation failed."),
            null,
            null,
            null);

    public static ExecutionResult Failed(
        ExecutionFailureKind kind,
        string code,
        string message,
        Exception? exception = null,
        ExecutionPlan? plan = null,
        IEnumerable<ExecutionStepResult>? steps = null,
        WorkflowRunState? workflow = null) =>
        new(
            kind is ExecutionFailureKind.Syntax or ExecutionFailureKind.Binding or ExecutionFailureKind.Validation
                ? ValidationResult.Failure(message)
                : ValidationResult.Success(),
            null,
            new ExecutionError(kind, code, message, exception),
            plan,
            steps,
            workflow);

    public static ExecutionResult Failed(string message) =>
        Failed(ExecutionFailureKind.Internal, "FLN999", message);
}
