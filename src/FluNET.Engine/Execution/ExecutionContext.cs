using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Language.Binding;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;

namespace FluNET.Execution
{
    /// <summary>
    /// Contains all data and state for a single execution pipeline.
    /// Passed through the chain of execution steps.
    /// </summary>
    public class ExecutionContext
    {
        public ProcessedPrompt Prompt { get; }
        public IReadOnlyList<TokenTree> CommandTrees { get; internal set; } = Array.Empty<TokenTree>();
        public IReadOnlyList<BoundCommand> BoundCommands { get; internal set; } = Array.Empty<BoundCommand>();
        public ExecutionPlan? Plan { get; internal set; }
        internal List<ExecutionStepResult> CompletedSteps { get; } = [];
        public IReadOnlyList<ExecutionStepResult> StepResults => CompletedSteps;

        /// <summary>
        /// Compatibility view of the first command. New pipeline stages should use
        /// <see cref="CommandTrees"/> so command boundaries are never reparsed.
        /// </summary>
        public TokenTree? TokenTree { get; set; }
        public ValidationResult? ValidationResult { get; set; }
        public ISentence? Sentence { get; set; }
        public object? Result { get; set; }
        public Exception? Exception { get; set; }
        public CancellationToken CancellationToken { get; internal set; }

        /// <summary>
        /// Additional data that can be used by execution steps
        /// </summary>
        public Dictionary<string, object> Data { get; } = new();

        public WorkflowRunState Workflow { get; }

        public ExecutionContext(
            ProcessedPrompt prompt,
            WorkflowExecutionOptions? workflowOptions = null)
        {
            Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
            Workflow = new WorkflowRunState(workflowOptions);
        }

        /// <summary>
        /// Checks if the execution should be aborted (validation failed or exception occurred)
        /// </summary>
        public bool ShouldAbort =>
            (ValidationResult != null && !ValidationResult.IsValid) ||
            Exception != null;
    }
}
