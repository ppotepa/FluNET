using FluNET.Compilation;
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
            if (context.Program is null)
            {
                return ValueTask.FromResult(ExecutionResult.Failed(
                    ExecutionFailureKind.Internal,
                    "FLN202",
                    "No parsed program is available for semantic binding."));
            }

            IReadOnlyList<BoundCommand> commands = binder.BindProgram(context.Program.Syntax);
            context.BoundProgram = BoundProgram.FromCommands(context.Program, commands);
            context.BoundCommands = context.BoundProgram.Commands;
            return next(context, cancellationToken);
        }
        catch (SemanticBindingException exception)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Binding,
                CompilationDiagnosticCodes.BindingFailure,
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
