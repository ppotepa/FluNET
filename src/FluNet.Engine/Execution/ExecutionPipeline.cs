namespace FluNET.Execution
{
    /// <summary>
    /// Manages the execution pipeline and orchestrates the execution steps.
    /// Implements the Chain of Responsibility pattern.
    /// </summary>
    public class ExecutionPipeline
    {
        private readonly List<IExecutionStep> _steps = new();

        /// <summary>
        /// Adds a step to the execution pipeline
        /// </summary>
        public ExecutionPipeline AddStep(IExecutionStep step)
        {
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// Executes the pipeline with the given context
        /// </summary>
        public ExecutionResult Execute(ExecutionContext context)
        {
            // Build the chain of responsibility
            Func<ExecutionContext, ExecutionResult> chain = BuildChain();

            // Execute the chain
            return chain(context);
        }

        /// <summary>
        /// Builds the execution chain from the registered steps
        /// </summary>
        private Func<ExecutionContext, ExecutionResult> BuildChain()
        {
            // Start with the terminal step (returns the result)
            Func<ExecutionContext, ExecutionResult> chain = TerminalStep;

            // Build the chain in reverse order
            for (int i = _steps.Count - 1; i >= 0; i--)
            {
                var currentStep = _steps[i];
                var nextStep = chain;

                chain = (context) => currentStep.Execute(context, nextStep);
            }

            return chain;
        }

        /// <summary>
        /// The terminal step that returns the final result
        /// </summary>
        private static ExecutionResult TerminalStep(ExecutionContext context)
        {
            // If we got here, execution completed
            if (context.ValidationResult != null && !context.ValidationResult.IsValid)
            {
                return ExecutionResult.Failed(context.ValidationResult);
            }

            if (context.Exception != null)
            {
                return ExecutionResult.Failed($"Execution failed: {context.Exception.Message}");
            }

            return ExecutionResult.Success(context.Sentence!, context.Result);
        }
    }
}
