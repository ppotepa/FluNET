using FluNET.Syntax.Core;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Words;

namespace FluNET.Syntax.Validation
{
    /// <summary>
    /// Service class responsible for validating complete sentences.
    /// Ensures sentences have proper structure, valid terminators, and correct word ordering.
    /// </summary>
    public class SentenceValidator(Lexicon.Lexicon lexicon, WordFactory wordFactory)
    {
        /// <summary>
        /// Validates a complete sentence represented as a token tree.
        /// Checks for:
        /// - Non-empty sentence
        /// - Valid terminator (., ?, or !)
        /// - Known verbs and words
        /// - Correct grammatical structure using IValidatable
        /// </summary>
        /// <param name="tokenTree">The token tree representing the sentence to validate</param>
        /// <returns>A ValidationResult indicating success or failure with detailed error messages</returns>
        public ValidationResult ValidateSentence(TokenTree tokenTree)
        {
            Token? current = tokenTree.Root;

            if (current == null || current.Type == TokenType.Terminal)
            {
                return ValidationResult.Failure("Empty sentence");
            }

            // Check if the sentence ends with a valid terminator (., ?, !)
            Token lastToken = current;
            while (lastToken.Next != null && lastToken.Next.Type != TokenType.Terminal)
            {
                lastToken = lastToken.Next;
            }

            string lastTokenValue = lastToken.Value.TrimEnd();
            if (!lastTokenValue.EndsWith('.') && !lastTokenValue.EndsWith('?') && !lastTokenValue.EndsWith('!'))
            {
                return ValidationResult.Failure(
                    "Invalid sentence: must end with a terminator (., ?, or !)");
            }

            // Convert first token (verb) to word
            IWord? verbWord = wordFactory.CreateWord(current);
            if (verbWord == null)
            {
                return ValidationResult.Failure($"Unknown verb: '{current.Value}'");
            }

            // First word must be a verb
            if (verbWord is not IVerb)
            {
                return ValidationResult.Failure($"Sentence must start with a verb, got: '{current.Value}'");
            }

            if (verbWord is not IValidatable currentValidator)
            {
                return ValidationResult.Failure($"Verb '{current.Value}' does not support validation");
            }

            // Validate subsequent words
            IWord? previousWord = verbWord; // Track the previous word for linking
            while (current.Next != null)
            {
                Token nextToken = current.Next;

                // If next token is a terminal, validate that current word can end the sentence
                if (nextToken.Type == TokenType.Terminal)
                {
                    // Create a special "terminator" word to validate against
                    IWord terminatorWord = new LiteralWord(nextToken.Value.TrimEnd('.', '?', '!'));
                    terminatorWord.Previous = previousWord; // Link it to the chain

                    ValidationResult terminatorResult = currentValidator.ValidateNext(terminatorWord, lexicon);

                    if (!terminatorResult.IsValid)
                    {
                        return terminatorResult;
                    }

                    break; // Sentence ends here
                }

                // Convert next token to word
                IWord? nextWord = wordFactory.CreateWord(nextToken);
                if (nextWord == null)
                {
                    return ValidationResult.Failure($"Unknown word: '{nextToken.Value}'");
                }

                // Link the words together
                nextWord.Previous = previousWord;
                if (previousWord != null)
                {
                    previousWord.Next = nextWord;
                }

                // Validate using interface-based checking
                ValidationResult result = currentValidator.ValidateNext(nextWord, lexicon);

                if (!result.IsValid)
                {
                    return result;
                }

                // Move to next word if it's also validatable
                if (nextWord is IValidatable validatable)
                {
                    currentValidator = validatable;
                }

                previousWord = nextWord; // Update previous word for next iteration
                current = nextToken;
            }

            return ValidationResult.Success();
        }
    }
}