using FluNET.Syntax.Validation;

namespace FluNET.Execution.Steps
{
    /// <summary>
    /// Step 2: Validate the token tree structure
    /// </summary>
    public class ValidationStep : IExecutionStep
    {
        private readonly SentenceValidator _sentenceValidator;

        public ValidationStep(SentenceValidator sentenceValidator)
        {
            _sentenceValidator = sentenceValidator ?? throw new ArgumentNullException(nameof(sentenceValidator));
        }

        public ExecutionResult Execute(ExecutionContext context, Func<ExecutionContext, ExecutionResult> next)
        {
            if (context.TokenTree == null)
            {
                return ExecutionResult.Failed("No token tree available for validation");
            }

            try
            {
                // Validate the token tree
                context.ValidationResult = _sentenceValidator.ValidateSentence(context.TokenTree);

                // If validation failed, abort the pipeline
                if (!context.ValidationResult.IsValid)
                {
                    return ExecutionResult.Failed(context.ValidationResult);
                }

                // Continue to next step
                return next(context);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                return ExecutionResult.Failed($"Validation failed: {ex.Message}");
            }
        }
    }
}
