using FluNET.Sentences;
using FluNET.Capabilities;
using FluNET.Syntax.Registry;

namespace FluNET.Execution.Steps;

public sealed class SubSentenceExecutionStep(
    SentenceExecutor sentenceExecutor,
    VariableStorageStep variableStorageStep) : IExecutionStep
{
    public async ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        if (context.Sentence is null)
        {
            return await next(context, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            for (int index = 0; index < context.Sentence.SubSentences.Count; index++)
            {
                ISentence subSentence = context.Sentence.SubSentences[index];
                int syntaxIndex = index + 1;
                var boundCommand = syntaxIndex < context.BoundCommands.Count
                    ? context.BoundCommands[syntaxIndex]
                    : null;
                object? subResult = await sentenceExecutor.ExecuteAsync(
                        subSentence,
                        boundCommand,
                        cancellationToken)
                    .ConfigureAwait(false);
                context.Result = subResult;

                if (subResult is not null && subSentence.Root is not null)
                {
                    ExecutionContext temporary = new(context.Prompt)
                    {
                        Sentence = subSentence,
                        Result = subResult
                    };

                    ExecutionResult storageResult = await variableStorageStep.ExecuteAsync(
                        temporary,
                        (value, token) => ValueTask.FromResult(ExecutionResult.Success(value.Sentence!, value.Result)),
                        cancellationToken).ConfigureAwait(false);

                    if (!storageResult.IsSuccess)
                    {
                        return storageResult;
                    }
                }
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
                $"Chained command activation failed: {exception.Message}",
                exception,
                context.Sentence);
        }
        catch (CapabilityDeniedException exception)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Capability,
                "FLN230",
                $"Chained command capability denied: {exception.Message}",
                exception,
                context.Sentence);
        }
        catch (Exception exception)
        {
            return ExecutionResult.Failed(
                ExecutionFailureKind.Execution,
                "FLN200",
                $"Chained command failed: {exception.Message}",
                exception,
                context.Sentence);
        }
    }
}
