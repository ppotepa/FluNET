namespace FluNET.Syntax
{
    /// <summary>
    /// Interface for sentence parts that can validate what follows them
    /// </summary>
    public interface IValidatable
    {
        /// <summary>
        /// Validates if the next token in the sentence is valid following this one
        /// </summary>
        /// <param name="nextTokenValue">The value of the next token</param>
        /// <param name="discoveryService">Service to discover available words</param>
        /// <returns>Validation result with reason if failed</returns>
        ValidationResult ValidateNext(string nextTokenValue, DiscoveryService discoveryService);
    }
}
