using FluNET.Sentences;

namespace FluNET.Execution.Steps
{
    /// <summary>
    /// Step 3: Create a sentence object from the validated token tree
    /// </summary>
    public class SentenceCreationStep : IExecutionStep
    {
        private readonly SentenceFactory _sentenceFactory;

        public SentenceCreationStep(SentenceFactory sentenceFactory)
        {
            _sentenceFactory = sentenceFactory ?? throw new ArgumentNullException(nameof(sentenceFactory));
        }

        public ExecutionResult Execute(ExecutionContext context, Func<ExecutionContext, ExecutionResult> next)
        {
            if (context.TokenTree == null)
            {
                return ExecutionResult.Failed("No token tree available for sentence creation");
            }

            try
            {
                // Create sentence from the validated tree
                context.Sentence = _sentenceFactory.CreateFromTree(context.TokenTree);

                if (context.Sentence == null)
                {
                    return ExecutionResult.Failed("Failed to create sentence from validated tree");
                }

                // Continue to next step
                return next(context);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                return ExecutionResult.Failed($"Sentence creation failed: {ex.Message}");
            }
        }
    }
}
