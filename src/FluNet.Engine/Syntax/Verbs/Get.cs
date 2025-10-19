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
        /// Gets the action function that performs the retrieval operation.
        /// </summary>
        public abstract Func<TFrom, TWhat> Act { get; }

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
        /// For GET verbs, expects either a FROM keyword or a WHAT noun.
        /// </summary>
        /// <param name="nextWord">The word following this verb</param>
        /// <param name="lexicon">The lexicon for advanced validation</param>
        /// <returns>ValidationResult indicating if the next word is valid</returns>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // Check if it's a FROM keyword using interface check
            if (nextWord is IFrom<TFrom>)
            {
                return ValidationResult.Success();
            }

            // Check if it's any IWhat noun - we can use the Lexicon to verify compatibility later if needed
            return nextWord is IWhat<TWhat>
                ? ValidationResult.Success()
                : ValidationResult.Failure(
                $"Invalid word after GET verb. Expected FROM keyword or a valid noun.");
        }

        /// <summary>
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
            var result = Execute();
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
    }
}
