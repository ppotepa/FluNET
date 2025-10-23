using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;

namespace FluNET.Execution
{
    /// <summary>
    /// Contains all data and state for a single execution pipeline.
    /// Passed through the chain of execution steps.
    /// </summary>
    public class ExecutionContext
    {
        public ProcessedPrompt Prompt { get; }
        public TokenTree? TokenTree { get; set; }
        public ValidationResult? ValidationResult { get; set; }
        public ISentence? Sentence { get; set; }
        public object? Result { get; set; }
        public Exception? Exception { get; set; }

        /// <summary>
        /// Additional data that can be used by execution steps
        /// </summary>
        public Dictionary<string, object> Data { get; } = new();

        public ExecutionContext(ProcessedPrompt prompt)
        {
            Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        }

        /// <summary>
        /// Checks if the execution should be aborted (validation failed or exception occurred)
        /// </summary>
        public bool ShouldAbort =>
            (ValidationResult != null && !ValidationResult.IsValid) ||
            Exception != null;
    }
}
