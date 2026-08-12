using FluNET.Syntax.Validation;

namespace FluNET.Execution.Steps;

public sealed class ValidationStep(SentenceValidator sentenceValidator) : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        if (context.TokenTree is null)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
                "No token tree is available for validation."));
        }

        context.ValidationResult = sentenceValidator.ValidateSentence(context.TokenTree);
        return context.ValidationResult.IsValid
            ? next(context, cancellationToken)
            : ValueTask.FromResult(ExecutionResult.Failed(context.ValidationResult));
    }
}
