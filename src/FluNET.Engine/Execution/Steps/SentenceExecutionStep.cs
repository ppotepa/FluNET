using FluNET.Sentences;
using FluNET.Syntax.Registry;
using FluNET.Capabilities;

namespace FluNET.Execution.Steps;

public sealed class SentenceExecutionStep(SentenceExecutor sentenceExecutor) : IExecutionStep
{
    public async ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        if (context.Sentence is null)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
                "No sentence is available for execution.");
        }

        try
        {
            context.Result = await sentenceExecutor.ExecuteAsync(context.Sentence, cancellationToken)
                .ConfigureAwait(false);
            if (context.Result is { } mainResult)
            {
                context.Data["MainResult"] = mainResult;
            }
            return await next(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Cancelled,
                "FLN201",
                "Execution was cancelled.",
                exception,
                context.Sentence);
        }
        catch (VerbActivationException exception)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Activation,
                "FLN210",
                exception.Message,
                exception,
                context.Sentence);
        }
        catch (CapabilityDeniedException exception)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Capability,
                "FLN230",
                exception.Message,
                exception,
                context.Sentence);
        }
        catch (Exception exception)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Execution,
                "FLN200",
                exception.Message,
                exception,
                context.Sentence);
        }
    }
}
