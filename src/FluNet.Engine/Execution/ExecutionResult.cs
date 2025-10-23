using FluNET.Sentences;
using FluNET.Syntax.Validation;

namespace FluNET.Execution
{
    /// <summary>
    /// Represents the result of an execution pipeline.
    /// </summary>
    public class ExecutionResult
    {
        public ValidationResult ValidationResult { get; }
        public ISentence? Sentence { get; }
        public object? Result { get; }

        public ExecutionResult(ValidationResult validationResult, ISentence? sentence, object? result)
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
            Sentence = sentence;
            Result = result;
        }

        /// <summary>
        /// Creates a successful execution result
        /// </summary>
        public static ExecutionResult Success(ISentence sentence, object? result)
        {
            return new ExecutionResult(Syntax.Validation.ValidationResult.Success(), sentence, result);
        }

        /// <summary>
        /// Creates a failed execution result
        /// </summary>
        public static ExecutionResult Failed(ValidationResult validationResult)
        {
            return new ExecutionResult(validationResult, null, null);
        }

        /// <summary>
        /// Creates a failed execution result with a message
        /// </summary>
        public static ExecutionResult Failed(string message)
        {
            return new ExecutionResult(Syntax.Validation.ValidationResult.Failure(message), null, null);
        }
    }
}
