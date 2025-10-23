using FluNET.Sentences;

namespace FluNET.Execution.Steps
{
    /// <summary>
    /// Step 4: Execute the main sentence and all sub-sentences (THEN clauses)
    /// </summary>
    public class SentenceExecutionStep : IExecutionStep
    {
        private readonly SentenceExecutor _sentenceExecutor;

        public SentenceExecutionStep(SentenceExecutor sentenceExecutor)
        {
            _sentenceExecutor = sentenceExecutor ?? throw new ArgumentNullException(nameof(sentenceExecutor));
        }

        public ExecutionResult Execute(ExecutionContext context, Func<ExecutionContext, ExecutionResult> next)
        {
            if (context.Sentence == null)
            {
                return ExecutionResult.Failed("No sentence available for execution");
            }

            try
            {
                // Execute main sentence
                context.Result = _sentenceExecutor.Execute(context.Sentence);

                // Store intermediate results for sub-sentence processing
                context.Data["MainResult"] = context.Result;

                // Continue to next step (variable storage, sub-sentence execution, etc.)
                return next(context);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                return ExecutionResult.Failed($"Execution failed: {ex.Message}");
            }
        }
    }
}
