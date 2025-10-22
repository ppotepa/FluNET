using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for GET verbs that retrieve data from a source.
    /// The GET verb follows the pattern: GET [what] FROM [source]
    /// </summary>
    /// <typeparam name="TWhat">The type of data being retrieved (e.g., string[], byte[])</typeparam>
    /// <typeparam name="TFrom">The type of the source from which data is retrieved (e.g., FileInfo, Uri)</typeparam>
    public abstract class Get<TWhat, TFrom> : IVerb<TWhat, TFrom>,
        IWhat<TWhat>,
        IFrom<TFrom>
    {
        /// <summary>
        /// Initializes a new instance of the Get verb.
        /// </summary>
        /// <param name="what">The data being retrieved</param>
        /// <param name="from">The source from which to retrieve data</param>
        protected Get(TWhat what, TFrom from)
        {
            this.What = what;
            this.From = from;
        }

        /// <summary>
        /// Gets or sets the data being retrieved.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the source from which data is retrieved.
        /// </summary>
        public TFrom From { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "GET";

        /// <summary>
        /// Gets the synonyms for this verb.
        /// These alternative keywords have exactly the same implementation as the main verb.
        /// All are FROM-based retrieval verbs (data flows FROM source TO variable).
        /// LOAD is a separate verb (LoadConfig) with its own implementation.
        /// </summary>
        public virtual string[] Synonyms => new[] { "FETCH", "RETRIEVE" };

        /// <summary>
        /// Gets the action function that performs the retrieval operation.
        /// </summary>
        public abstract Func<TFrom, TWhat> Act { get; }

        /// <summary>
        /// Validates whether this verb implementation can handle the given word.
        /// Derived classes must implement this to validate parameters like FROM sources.
        /// </summary>
        public abstract bool Validate(IWord word);

        /// <summary>
        /// Resolves a string value to the TFrom type contextually.
        /// This is where plugins define their resolution logic (file path → FileInfo, URL → Uri, etc.)
        /// </summary>
        /// <param name="value">The string value to resolve</param>
        /// <returns>The resolved TFrom instance, or null if resolution fails</returns>
        public abstract TFrom? Resolve(string value);

        /// <summary>
        /// Determines if this verb can handle the given sentence structure.
        /// Checks for the FROM preposition and validates its value.
        /// </summary>
        /// <param name="root">The root word of the sentence</param>
        /// <returns>True if this verb can handle the sentence structure</returns>
        public virtual bool CanHandle(IWord root)
        {
            // Find the FROM keyword (not the interface, the actual keyword class)
            Keywords.From? fromPrep = root.Find<Keywords.From>();
            if (fromPrep == null)
            {
                return false;
            }

            // The FROM preposition should have a value after it
            IWord? valueWord = fromPrep.Next;
            if (valueWord == null)
            {
                return false;
            }

            // Validate the value using the derived class's validation logic
            return Validate(valueWord);
        }

        // IWord navigation properties
        /// <summary>
        /// Gets or sets the next word in the sentence chain.
        /// </summary>
        public IWord? Next { get; set; }

        /// <summary>
        /// Gets or sets the previous word in the sentence chain.
        /// </summary>
        public IWord? Previous { get; set; }

        /// <summary>
        /// Validates that the next word in the sentence is grammatically correct.
        /// For GET verbs, the first word after GET can be:
        /// - A qualifier (TEXT, JSON, XML, etc.) followed by [what]
        /// - Directly [what] (variable or reference)
        /// NOT the FROM keyword directly.
        /// </summary>
        /// <param name="nextWord">The word following this verb</param>
        /// <param name="lexicon">The lexicon for advanced validation</param>
        /// <returns>ValidationResult indicating if the next word is valid</returns>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // GET must be followed by [what] - either a variable or reference
            // "GET FROM {file}" is INVALID - missing the WHAT
            if (nextWord is Keywords.From)
            {
                return ValidationResult.Failure(
                    "GET verb requires a subject (what to get). Expected [variable] or {reference} before FROM.");
            }

            // Accept qualifier (like TEXT, JSON, XML) which will be followed by [what]
            if (nextWord is Words.QualifierWord)
            {
                // Qualifier is valid - the qualifier will validate its next word
                return ValidationResult.Success();
            }

            // Accept variable, reference, or IWhat noun as the [what] parameter
            bool isValidWhat = nextWord is Words.VariableWord
                            || nextWord is Words.ReferenceWord
                            || nextWord is IWhat<TWhat>;

            if (!isValidWhat)
            {
                return ValidationResult.Failure(
                    $"Invalid word after GET verb. Expected qualifier, [variable], or {{reference}} specifying what to get.");
            }

            // Now validate that FROM keyword follows the [what] word
            // Since Get implements IFrom<TFrom>, FROM is required
            // We'll check this when the [what] word validates its next word
            return ValidationResult.Success();
        }        /// <summary>

                 /// Executes the GET operation and returns the retrieved data.
                 /// </summary>
                 /// <returns>The data retrieved from the source</returns>
        public virtual TWhat Execute()
        {
            return Act(From);
        }

        /// <summary>
        /// Creates a THEN chain that passes the retrieved data to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the retrieved data</returns>
        public virtual IThen<TWhat> Then()
        {
            TWhat? result = Invoke();
            return new ThenKeyword<TWhat>(result);
        }
    }

    /// <summary>
    /// Implementation of the THEN keyword that carries data between verb operations.
    /// </summary>
    /// <typeparam name="TData">The type of data being passed through the chain</typeparam>
    internal class ThenKeyword<TData>(TData data) : IThen<TData>
    {
        public TData Data { get; } = data;
        public string Text => "THEN";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }

        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // THEN must be followed by a verb
            return nextWord is IVerb
                ? ValidationResult.Success()
                : ValidationResult.Failure("THEN must be followed by a verb");
        }

        public bool Validate(IWord word)
        {
            // THEN doesn't need to validate specific parameters
            return true;
        }
    }
}