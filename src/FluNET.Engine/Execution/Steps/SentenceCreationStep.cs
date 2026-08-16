using FluNET.Sentences;

namespace FluNET.Execution.Steps;

public sealed class SentenceCreationStep(SentenceFactory sentenceFactory) : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
<<<<<<< HEAD
        if (context.CommandTrees.Count == 0)
=======
        if (context.TokenTree is null)
>>>>>>> origin/agent/stabilize-poc-foundation
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
<<<<<<< HEAD
                "No parsed commands are available for sentence creation."));
=======
                "No token tree is available for sentence creation."));
>>>>>>> origin/agent/stabilize-poc-foundation
        }

        try
        {
<<<<<<< HEAD
            context.Sentence = sentenceFactory.CreateFromTrees(context.CommandTrees);
=======
            context.Sentence = sentenceFactory.CreateFromTree(context.TokenTree);
>>>>>>> origin/agent/stabilize-poc-foundation
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
