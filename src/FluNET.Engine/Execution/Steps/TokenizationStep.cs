using FluNET.Prompt;
using FluNET.Tokens.Tree;

namespace FluNET.Execution.Steps;

public sealed class TokenizationStep(TokenTreeFactory tokenTreeFactory) : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.CancellationToken = cancellationToken;
            context.CommandTrees = tokenTreeFactory.ProcessCommands(context.Prompt);
            context.TokenTree = context.CommandTrees.FirstOrDefault();
            return next(context, cancellationToken);
        }
        catch (PromptSyntaxException exception)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Syntax,
                exception.Diagnostics.FirstOrDefault()?.Code ?? "FLN001",
                exception.Message,
                exception));
        }
        catch (OperationCanceledException exception)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Cancelled,
                "FLN201",
                "Execution was cancelled.",
                exception));
        }
    }
}
