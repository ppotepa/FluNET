namespace FluNET.Syntax
{
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string? FailureReason { get; }

        private ValidationResult(bool isValid, string? failureReason = null)
        {
            IsValid = isValid;
            FailureReason = failureReason;
        }

        public static ValidationResult Success() => new ValidationResult(true);

        public static ValidationResult Failure(string reason) =>
            new ValidationResult(false, reason);
    }
}
