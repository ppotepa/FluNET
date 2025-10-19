using FluNET.Keywords;
using FluNET.Tokens;
using FluNET.Variables;

namespace FluNET.Words
{
    public class WordFactory
    {
        private readonly DiscoveryService _discoveryService;

        public WordFactory(DiscoveryService discoveryService)
        {
            _discoveryService = discoveryService;
        }

        /// <summary>
        /// Creates a word from a token. If the token is a variable reference [name],
        /// it creates a VariableWord placeholder that will be resolved later.
        /// If the token is a reference {resource}, it creates a ReferenceWord.
        /// If no matching keyword is found, it creates a LiteralWord for literal values.
        /// </summary>
        public IWord? CreateWord(Token token)
        {
            // Check if this token is a variable reference [name]
            if (token.Type == TokenType.Variable || VariableResolver.IsVariableReference(token.Value))
            {
                return new VariableWord(token.Value);
            }

            // Check if this token is a reference {resource}
            if (token.Type == TokenType.Reference || (token.Value.StartsWith("{") && token.Value.EndsWith("}")))
            {
                return new ReferenceWord(token.Value);
            }

            IReadOnlyList<Type> allWords = _discoveryService.Words;

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

        public bool Validate(IWord word)
        {
            // Variables are validated by the verb implementations
            return true;
        }
    }

    /// <summary>
    /// Represents a reference to an external resource (file path, URL, endpoint, etc.).
    /// References use {reference} syntax in prompts.
    /// </summary>
    public class ReferenceWord : IWord, IKeyword, IValidatable
    {
        public ReferenceWord(string reference)
        {
            // Strip the braces and whitespace if present
            Reference = reference.Trim('{', '}', ' ');
        }

        public string Reference { get; }

        public string Text => $"{{{Reference}}}";

        public IWord? Next { get; set; }

        public IWord? Previous { get; set; }

        /// <summary>
        /// Resolves the reference to a specific type (FileInfo, Uri, etc.).
        /// </summary>
        public T? ResolveAs<T>() where T : class
        {
            if (typeof(T) == typeof(FileInfo))
            {
                return new FileInfo(Reference) as T;
            }
            else if (typeof(T) == typeof(Uri))
            {
                return Uri.TryCreate(Reference, UriKind.Absolute, out Uri? uri) ? uri as T : null;
            }
            else if (typeof(T) == typeof(string))
            {
                return Reference as T;
            }
            return null;
        }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // References can be followed by any word (keywords or other references)
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            // References are validated by the verb implementations
            return true;
        }

        public override string ToString() => $"ReferenceWord: {{{Reference}}}";
    }

    /// <summary>
    /// Represents a qualifier word (TEXT, JSON, XML, BINARY, etc.).
    /// Qualifiers specify the type or format of data being operated on.
    /// </summary>
    public class QualifierWord : IWord, IKeyword, IValidatable
    {
        public QualifierWord(string qualifier)
        {
            Qualifier = qualifier.ToUpperInvariant();
        }

        public string Qualifier { get; }

        public string Text => Qualifier;

        public IWord? Next { get; set; }

        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // Qualifiers are typically followed by variables or other keywords
            return ValidationResult.Success();
        }

        public bool Validate(IWord word)
        {
            // Qualifiers are validated by the verb implementations
            return true;
        }

        public override string ToString() => $"QualifierWord: {Qualifier}";
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

        public bool Validate(IWord word)
        {
            // Literals are validated by the verb implementations
            return true;
        }
    }
}