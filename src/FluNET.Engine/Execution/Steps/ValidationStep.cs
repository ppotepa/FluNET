using FluNET.Syntax.Validation;

namespace FluNET.Execution.Steps;

public sealed class ValidationStep(SentenceValidator sentenceValidator) : IExecutionStep
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
                "No parsed commands are available for validation."));
        }

        context.ValidationResult = sentenceValidator.ValidateCommands(context.CommandTrees);
        return context.ValidationResult.IsValid
            ? next(context, cancellationToken)
            : ValueTask.FromResult(ExecutionResult.Failed(context.ValidationResult));
    }
}
