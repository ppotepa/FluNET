using FluNET.Capabilities;
using FluNET.Execution.Planning;

namespace FluNET.Execution.Steps;

public sealed class PlanExecutionStep(ExecutionPlanExecutor executor) : IExecutionStep
{
    public async ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        if (context.Plan is null)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
                "No execution plan is available.",
                sentence: context.Sentence);
        }

        try
        {
            context.Result = await executor.ExecuteAsync(
                context.Plan,
                context.CompletedSteps,
                cancellationToken).ConfigureAwait(false);
            return await next(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return Failure(ExecutionFailureKind.Cancelled, "FLN201", "Execution was cancelled.", exception, context);
        }
        catch (CommandRouteNotFoundException exception)
        {
            return Failure(ExecutionFailureKind.Activation, "FLN210", exception.Message, exception, context);
        }
        catch (CapabilityDeniedException exception)
        {
            return Failure(ExecutionFailureKind.Capability, "FLN230", exception.Message, exception, context);
        }
        catch (Exception exception)
        {
            return Failure(ExecutionFailureKind.Execution, "FLN200", exception.Message, exception, context);
        }
    }

    private static ExecutionResult Failure(
        ExecutionFailureKind kind,
        string code,
        string message,
        Exception exception,
        ExecutionContext context) =>
        ExecutionResult.Failed(
            kind,
            code,
            message,
            exception,
            context.Sentence,
            context.Plan,
            context.StepResults);
}
