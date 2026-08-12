using FluNET.Language.Binding;

namespace FluNET.Execution.Steps;

public sealed class SemanticBindingStep(SemanticCommandBinder binder) : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.BoundCommands = binder.BindProgram(context.Prompt.Syntax);
            return next(context, cancellationToken);
        }
        catch (SemanticBindingException exception)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Binding,
                "FLN110",
                exception.Message,
                exception));
        }
        catch (OperationCanceledException exception)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Cancelled,
                "FLN201",
                "Execution was cancelled while binding command semantics.",
                exception));
        }
    }
}
