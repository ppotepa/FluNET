using FluNET.Sentences;

namespace FluNET.Execution.Steps
{
    /// <summary>
    /// Step 6: Execute sub-sentences (THEN clauses)
    /// All sub-sentences share the same variable context
    /// </summary>
    public class SubSentenceExecutionStep : IExecutionStep
    {
        private readonly SentenceExecutor _sentenceExecutor;
        private readonly VariableStorageStep _variableStorageStep;

        public SubSentenceExecutionStep(SentenceExecutor sentenceExecutor, VariableStorageStep variableStorageStep)
        {
            _sentenceExecutor = sentenceExecutor ?? throw new ArgumentNullException(nameof(sentenceExecutor));
            _variableStorageStep = variableStorageStep ?? throw new ArgumentNullException(nameof(variableStorageStep));
        }

        public ExecutionResult Execute(ExecutionContext context, Func<ExecutionContext, ExecutionResult> next)
        {
            if (context.Sentence == null)
            {
                return next(context);
            }

            try
            {
                // Execute each sub-sentence (THEN clauses)
                foreach (ISentence subSentence in context.Sentence.SubSentences)
                {
                    // Execute sub-sentence
                    object? subResult = _sentenceExecutor.Execute(subSentence);

                    // Update result to the last sub-sentence result
                    context.Result = subResult;

                    // Store result in variable if needed
                    if (subResult != null && subSentence.Root != null)
                    {
                        // Create a temporary context for variable storage
                        var tempContext = new ExecutionContext(context.Prompt)
                        {
                            Sentence = subSentence,
                            Result = subResult
                        };

                        // Use the variable storage step
                        _variableStorageStep.Execute(tempContext, (ctx) => ExecutionResult.Success(ctx.Sentence!, ctx.Result));
                    }
                }

                // Continue to next step
                return next(context);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                return ExecutionResult.Failed($"Sub-sentence execution failed: {ex.Message}");
            }
        }
    }
}
