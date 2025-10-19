using FluNET.Keywords;
using FluNET.Tokens;

namespace FluNET.Syntax
{
    /// <summary>
    /// Validates sentence structure by recursively calling word validators
    /// </summary>
    public class SentenceValidator
    {
        private readonly DiscoveryService discoveryService;

        public SentenceValidator(DiscoveryService discoveryService)
        {
            this.discoveryService = discoveryService;
        }

        /// <summary>
        /// Validates the entire sentence structure
        /// </summary>
        public ValidationResult ValidateSentence(TokenTree tokenTree)
        {
            // Start after the root token
            var current = tokenTree.Root?.Next;
            
            if (current == null || current.Type == TokenType.Terminal)
            {
                return ValidationResult.Failure("Empty sentence");
            }

            // First token should be a verb
            var verbToken = current;
            var verbType = discoveryService.Verbs.FirstOrDefault(v =>
            {
                try
                {
                    var instance = Activator.CreateInstance(v, new object[] { null, null });
                    return ((IKeyword)instance).Text.Equals(verbToken.Value, 
                        StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            });

            if (verbType == null)
            {
                return ValidationResult.Failure($"Unknown verb: '{verbToken.Value}'");
            }

            // Create verb instance to use for validation
            IValidatable currentValidator;
            try
            {
                currentValidator = (IValidatable)Activator.CreateInstance(verbType, 
                    new object[] { null, null });
            }
            catch
            {
                return ValidationResult.Failure($"Could not instantiate verb: '{verbToken.Value}'");
            }

            // Walk through tokens and validate each step
            while (current.Next != null && current.Next.Type != TokenType.Terminal)
            {
                var nextToken = current.Next;
                
                // Use current validator to check if next token is valid
                var result = currentValidator.ValidateNext(nextToken.Value, discoveryService);
                
                if (!result.IsValid)
                {
                    return result;
                }

                // Move to next token
                current = nextToken;
                
                // TODO: Find the next validator based on the current token
                // This would involve looking up noun types, prepositions, etc.
                // For now, we stop after validating the first step (verb -> noun)
                break;
            }

            return ValidationResult.Success();
        }
    }
}
