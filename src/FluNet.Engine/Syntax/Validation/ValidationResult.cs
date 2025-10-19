namespace FluNET.Syntax.Validation
{
    /// <summary>
    /// Immutable result object representing the outcome of a validation operation.
    /// Use static factory methods to create success or failure results.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets whether the validation succeeded.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the failure reason if validation failed, null if successful.
        /// </summary>
        public string? FailureReason { get; }

        private ValidationResult(bool isValid, string? failureReason = null)
        {
            IsValid = isValid;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        /// <returns>A ValidationResult indicating success</returns>
        public static ValidationResult Success() => new(true);

        /// <summary>
        /// Creates a failed validation result with a reason.
        /// </summary>
        /// <param name="reason">The reason for validation failure</param>
        /// <returns>A ValidationResult indicating failure</returns>
        public static ValidationResult Failure(string reason) =>
            new(false, reason);
    }
}