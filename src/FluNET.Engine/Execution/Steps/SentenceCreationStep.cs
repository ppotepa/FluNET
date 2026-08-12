using FluNET.Sentences;

namespace FluNET.Execution.Steps;

public sealed class SentenceCreationStep(SentenceFactory sentenceFactory) : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        if (context.CommandTrees.Count == 0)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
                "No parsed commands are available for sentence creation."));
        }

        try
        {
            context.Sentence = sentenceFactory.CreateFromTrees(context.CommandTrees);
            return context.Sentence is null
                ? ValueTask.FromResult(ExecutionResult.Failed(
                    ExecutionFailureKind.Internal,
                    "FLN202",
                    "Could not create a sentence from a validated prompt."))
                : next(context, cancellationToken);
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
                $"Sentence creation failed: {exception.Message}",
                exception));
        }
    }
}
