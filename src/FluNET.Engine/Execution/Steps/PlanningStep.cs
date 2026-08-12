using FluNET.Execution.Planning;

namespace FluNET.Execution.Steps;

public sealed class PlanningStep(ExecutionPlanner planner) : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Plan = planner.Create(context.BoundCommands, context.Prompt.Syntax);
            return next(context, cancellationToken);
        }
        catch (ExecutionPlanException exception)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Binding,
                "FLN120",
                exception.Message,
                exception,
                context.Sentence));
        }
    }
}
