using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Syntax.Registry;
using FluNET.Variables;

namespace FluNET.Words
{
    public class WordFactory
    {
        private readonly LanguageRegistry _languageRegistry;

        public WordFactory(DiscoveryService discoveryService)
        {
            ArgumentNullException.ThrowIfNull(discoveryService);
            _languageRegistry = discoveryService.Registry;
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
            if (token.Type == TokenType.Reference || (token.Value.StartsWith('{') && token.Value.EndsWith('}')))
            {
                return new ReferenceWord(token.Value);
            }

            // Qualifiers are declared by frames in the immutable language snapshot.
            if (_languageRegistry.Snapshot.IsQualifier(token.Value))
            {
                return new QualifierWord(token.Value);
            }

            IWord? registeredWord = _languageRegistry.CreateWord(token.Value);
            if (registeredWord is not null)
            {
                return registeredWord;
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
    public class VariableWord : IWord, IKeyword
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
            // If the previous word is a verb that implements IFrom (like GET, LOAD, DELETE),
            // then this variable MUST be followed by FROM keyword
            if (Previous != null)
            {
                // Check if previous is a verb that requires FROM (has IFrom in its interfaces)
                bool requiresFrom = Previous.GetType().GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IFrom"));

                if (requiresFrom && nextWord is not Keywords.From)
                {
                    // Check if next word is a terminator (empty literal or sentence-ending punctuation)
                    bool isTerminator = nextWord is LiteralWord literal &&
                                       (string.IsNullOrWhiteSpace(literal.Value) ||
                                        literal.Value == "." ||
                                        literal.Value == "?" ||
                                        literal.Value == "!");

                    string errorSuffix = isTerminator
                        ? " Sentence cannot end here."
                        : "";

                    return ValidationResult.Failure(
                        $"{Previous.GetType().Name.ToUpper()} [variable] must be followed by FROM keyword.{errorSuffix}");
                }
            }

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
    public class ReferenceWord : IWord, IKeyword
    {
        public ReferenceWord(string reference)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reference);
            string trimmed = reference.Trim();
            RawText = trimmed;
            Reference = trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[^1] == '}'
                ? trimmed[1..^1]
                : trimmed;
        }

        public string RawText { get; }

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
            // If the previous word is a verb that implements IFrom (like GET, LOAD, DELETE),
            // then this reference MUST be followed by FROM keyword
            if (Previous != null)
            {
                // Check if previous is a verb that requires FROM (has IFrom in its interfaces)
                bool requiresFrom = Previous.GetType().GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IFrom"));

                if (requiresFrom && nextWord is not Keywords.From)
                {
                    // Check if next word is a terminator (empty literal or sentence-ending punctuation)
                    bool isTerminator = nextWord is LiteralWord literal &&
                                       (string.IsNullOrWhiteSpace(literal.Value) ||
                                        literal.Value == "." ||
                                        literal.Value == "?" ||
                                        literal.Value == "!");

                    string errorSuffix = isTerminator
                        ? " Sentence cannot end here."
                        : "";

                    return ValidationResult.Failure(
                        $"{Previous.GetType().Name.ToUpper()} {{reference}} must be followed by FROM keyword.{errorSuffix}");
                }
            }

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
    public class QualifierWord : IWord, IKeyword
    {
        public QualifierWord(string qualifier)
        {
            OriginalText = qualifier;
            Qualifier = qualifier.ToUpperInvariant();
        }

        public string OriginalText { get; }

        public string Qualifier { get; }

        public string Text => Qualifier;

        public IWord? Next { get; set; }

        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // Historic LOAD syntax treats a lone frame selector as the direct
            // object: LOAD config FROM file. Keep that compatibility shape
            // while typed syntax also supports LOAD CONFIG [value] FROM file.
            if (nextWord is Keywords.From &&
                Previous is IVerb verb &&
                verb.Text.Equals("LOAD", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Success();
            }

            // If the previous word is a verb that implements IFrom (like GET, LOAD, DELETE),
            // then this qualifier MUST be followed by [what] (variable or reference)
            if (Previous != null)
            {
                // Check if previous is a verb that requires FROM (has IFrom in its interfaces)
                bool requiresFrom = Previous.GetType().GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IFrom"));

                if (requiresFrom)
                {
                    // Check if next word is a terminator
                    bool isTerminator = nextWord is LiteralWord literal &&
                                       (string.IsNullOrWhiteSpace(literal.Value) ||
                                        literal.Value == "." ||
                                        literal.Value == "?" ||
                                        literal.Value == "!");

                    // Qualifier must be followed by [what]
                    bool isValidWhat = nextWord is VariableWord || nextWord is ReferenceWord;

                    if (!isValidWhat || isTerminator)
                    {
                        string errorSuffix = isTerminator
                            ? " Sentence cannot end here."
                            : "";

                        return ValidationResult.Failure(
                            $"{Previous.GetType().Name.ToUpper()} {Qualifier} must be followed by [variable] or {{reference}}.{errorSuffix}");
                    }
                }
            }

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
