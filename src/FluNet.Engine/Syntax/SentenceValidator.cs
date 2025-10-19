using FluNET.Keywords;
using FluNET.Tokens;

namespace FluNET.Syntax
{
    public class SentenceValidator
    {
        private readonly DiscoveryService discoveryService;
        private readonly Lexicon.Lexicon lexicon;

        public SentenceValidator(DiscoveryService discoveryService, Lexicon.Lexicon lexicon)
        {
            this.discoveryService = discoveryService;
            this.lexicon = lexicon;
        }

        public ValidationResult ValidateSentence(TokenTree tokenTree)
        {
            var current = tokenTree.Root?.Next;

            if (current == null || current.Type == TokenType.Terminal)
            {
                return ValidationResult.Failure("Empty sentence");
            }

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

            while (current.Next != null && current.Next.Type != TokenType.Terminal)
            {
                var nextToken = current.Next;

                var result = currentValidator.ValidateNext(nextToken.Value, lexicon);

                if (!result.IsValid)
                {
                    return result;
                }

                current = nextToken;

                break;
            }

            return ValidationResult.Success();
        }
    }
}
