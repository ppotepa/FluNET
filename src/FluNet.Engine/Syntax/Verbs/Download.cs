using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for DOWNLOAD verbs that retrieve files from remote sources.
    /// The DOWNLOAD verb follows the pattern: DOWNLOAD [file] FROM [source] TO {destination}
    /// If TO is omitted, the filename is extracted from the source URI.
    /// </summary>
    /// <typeparam name="TWhat">The type of data being downloaded (e.g., byte[], FileInfo)</typeparam>
    /// <typeparam name="TFrom">The type of the source from which to download (e.g., Uri, string)</typeparam>
    /// <typeparam name="TTo">The type of the destination to save to (e.g., FileInfo, string)</typeparam>
    public abstract class Download<TWhat, TFrom, TTo> : IVerb<TWhat, TFrom>,
        IWhat<TWhat>,
        IFrom<TFrom>,
        ITo<TTo>
    {
        /// <summary>
        /// Initializes a new instance of the Download verb.
        /// </summary>
        /// <param name="what">The data being downloaded</param>
        /// <param name="from">The source from which to download</param>
        /// <param name="to">The destination to save to (optional)</param>
        protected Download(TWhat what, TFrom from, TTo? to = default)
        {
            What = what;
            From = from;
            To = to;
        }

        /// <summary>
        /// Gets or sets the data being downloaded.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the source from which to download.
        /// </summary>
        public TFrom From { get; protected set; }

        /// <summary>
        /// Gets or sets the destination to save to (optional).
        /// </summary>
        public TTo? To { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "DOWNLOAD";

        /// <summary>
        /// Gets the synonyms for this verb.
        /// These alternative keywords have exactly the same implementation as the main verb.
        /// </summary>
        public virtual string[] Synonyms => new[] { "PULL", "GRAB", "OBTAIN" };

        /// <summary>
        /// Gets the action function that performs the download operation.
        /// </summary>
        public abstract Func<TFrom, TWhat> Act { get; }

        /// <summary>
        /// Validates whether this verb implementation can handle the given word.
        /// Derived classes must implement this to validate parameters like FROM sources.
        /// </summary>
        public abstract bool Validate(IWord word);

        /// <summary>
        /// Resolves a string value to the TFrom type contextually.
        /// This is where implementations define their resolution logic (URL → Uri, etc.)
        /// </summary>
        /// <param name="value">The string value to resolve</param>
        /// <returns>The resolved TFrom instance, or null if resolution fails</returns>
        public abstract TFrom? Resolve(string value);

        /// <summary>
        /// Resolves a string value to the TTo type for the destination.
        /// </summary>
        /// <param name="value">The destination string value</param>
        /// <returns>The resolved TTo instance, or null if resolution fails</returns>
        public abstract TTo? ResolveTo(string value);

        /// <summary>
        /// Extracts the filename from the source if no destination is specified.
        /// </summary>
        /// <param name="source">The source from which to extract the filename</param>
        /// <returns>The extracted filename</returns>
        protected abstract string ExtractFilename(TFrom source);

        /// <summary>
        /// Determines if this verb can handle the given sentence structure.
        /// Checks for the FROM preposition and optionally the TO preposition.
        /// </summary>
        /// <param name="root">The root word of the sentence</param>
        /// <returns>True if this verb can handle the sentence structure</returns>
        public virtual bool CanHandle(IWord root)
        {
            // Find the FROM keyword (required)
            Keywords.From? fromPrep = root.Find<Keywords.From>();
            if (fromPrep == null)
            {
                return false;
            }

            // The FROM preposition should have a value after it
            IWord? fromValueWord = fromPrep.Next;
            if (fromValueWord == null)
            {
                return false;
            }

            // Validate the FROM value
            if (!Validate(fromValueWord))
            {
                return false;
            }

            // TO is optional - if present, validate it
            Keywords.To? toPrep = root.Find<Keywords.To>();
            if (toPrep != null)
            {
                IWord? toValueWord = toPrep.Next;
                if (toValueWord == null)
                {
                    return false;
                }
                // Validate TO value if present
                return Validate(toValueWord);
            }

            return true;
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
        /// For DOWNLOAD verbs, the first word after DOWNLOAD can be:
        /// - A qualifier (FILE, IMAGE, etc.) followed by [what]
        /// - Directly [what] (variable or reference)
        /// NOT the FROM keyword directly.
        /// </summary>
        /// <param name="nextWord">The word following this verb</param>
        /// <param name="lexicon">The lexicon for advanced validation</param>
        /// <returns>ValidationResult indicating if the next word is valid</returns>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // DOWNLOAD must be followed by [what] - either a variable or reference
            // "DOWNLOAD FROM {url}" is INVALID - missing the WHAT
            if (nextWord is Keywords.From)
            {
                return ValidationResult.Failure(
                    "DOWNLOAD verb requires a subject (what to download). Expected [variable] or {reference} before FROM.");
            }

            // Accept qualifier (like FILE, IMAGE, etc.) which will be followed by [what]
            if (nextWord is Words.QualifierWord)
            {
                return ValidationResult.Success();
            }

            // Accept variable, reference, or IWhat noun as the [what] parameter
            bool isValidWhat = nextWord is Words.VariableWord
                            || nextWord is Words.ReferenceWord
                            || nextWord is IWhat<TWhat>;

            if (!isValidWhat)
            {
                return ValidationResult.Failure(
                    $"Invalid word after DOWNLOAD verb. Expected qualifier, [variable], or {{reference}} specifying what to download.");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Invokes the DOWNLOAD operation and returns the downloaded data.
        /// </summary>
        /// <returns>The data downloaded from the source</returns>
        public virtual TWhat Invoke()
        {
            return Act(From);
        }
    }
}