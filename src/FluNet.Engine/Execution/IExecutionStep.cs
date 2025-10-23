namespace FluNET.Execution
{
    /// <summary>
    /// Represents a step in the execution pipeline.
    /// Each step can process the execution context and pass control to the next step.
    /// </summary>
    public interface IExecutionStep
    {
        /// <summary>
        /// Executes this step in the pipeline.
        /// </summary>
        /// <param name="context">The execution context containing all necessary data</param>
        /// <param name="next">The next step in the pipeline</param>
        /// <returns>The execution result</returns>
        ExecutionResult Execute(ExecutionContext context, Func<ExecutionContext, ExecutionResult> next);
    }
}
