using FluNET.Syntax.Validation;

namespace FluNET.Execution.Steps;

public sealed class ValidationStep(SentenceValidator sentenceValidator) : IExecutionStep
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
                "No parsed commands are available for validation."));
        }

        context.ValidationResult = sentenceValidator.ValidateCommands(context.CommandTrees);
=======
                "No token tree is available for validation."));
        }

        context.ValidationResult = sentenceValidator.ValidateSentence(context.TokenTree);
>>>>>>> origin/agent/stabilize-poc-foundation
        return context.ValidationResult.IsValid
            ? next(context, cancellationToken)
            : ValueTask.FromResult(ExecutionResult.Failed(context.ValidationResult));
    }
}
