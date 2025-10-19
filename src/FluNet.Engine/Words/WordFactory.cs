using FluNET.Keywords;
using FluNET.Syntax;
using FluNET.Tokens;
using FluNET.Variables;

namespace FluNET.Words
{
    public class WordFactory
    {
        /// <summary>
        /// Creates a word from a token. If the token is a variable reference [name],
        /// it creates a VariableWord placeholder that will be resolved later.
        /// If no matching keyword is found, it creates a LiteralWord for literal values.
        /// </summary>
        public IWord? CreateWord(Token token)
        {
            // Check if this token is a variable reference [name]
            if (VariableResolver.IsVariableReference(token.Value))
            {
                return new VariableWord(token.Value);
            }

            IReadOnlyList<Type> allWords = DiscoveryService.Words;

            foreach (Type wordType in allWords)
            {
                try
                {
                    // Try parameterless constructor first
                    object? instance = Activator.CreateInstance(wordType);
                    if (instance is IWord word && instance is IKeyword keyword &&
                        keyword.Text.Equals(token.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return word;
                    }
                }
                catch
                {
                    // Try with null parameters for backward compatibility
                    try
                    {
                        object? instance = Activator.CreateInstance(wordType, new object?[] { null, null });
                        if (instance is IWord word && instance is IKeyword keyword &&
                            keyword.Text.Equals(token.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            return word;
                        }
                    }
                    catch
                    {
                        // Silently continue if instantiation fails
                    }
                }
            }

            // If no keyword matches, treat it as a literal value
            return new LiteralWord(token.Value);
        }

        public bool TryCreateWord(Token token, out IWord? word)
        {
            word = CreateWord(token);
            return word != null;
        }
    }

    /// <summary>
    /// Represents a variable reference in a sentence.
    /// This is a placeholder that will be resolved during execution.
    /// </summary>
    public class VariableWord : IWord, IKeyword, IValidatable
    {
        public VariableWord(string variableReference)
        {
            VariableReference = variableReference;
        }

        public string VariableReference { get; }

        public string Text => VariableReference;

        public IWord? Next { get; set; }

        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // Variables can be followed by any word (keywords or other variables)
            return ValidationResult.Success();
        }
    }

    /// <summary>
    /// Represents a literal value in a sentence (file paths, strings, etc.).
    /// </summary>
    public class LiteralWord : IWord, IValidatable
    {
        public LiteralWord(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public IWord? Next { get; set; }

        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // Literals can be followed by keywords or end of sentence
            return ValidationResult.Success();
        }
    }
}