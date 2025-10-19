namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for LOAD verbs that retrieve and load data from a source.
    /// The LOAD verb follows the pattern: LOAD [data] FROM [source]
    /// Similar to GET but emphasizes loading into memory/state.
    /// </summary>
    /// <typeparam name="TWhat">The type of data being loaded</typeparam>
    /// <typeparam name="TFrom">The type of the source to load from</typeparam>
    public abstract class Load<TWhat, TFrom> : IVerb<TWhat, TFrom>,
        IWhat<TWhat>,
        IFrom<TFrom>
    {
        /// <summary>
        /// Initializes a new instance of the Load verb.
        /// </summary>
        /// <param name="what">The data being loaded</param>
        /// <param name="from">The source to load from</param>
        protected Load(TWhat what, TFrom from)
        {
            What = what;
            From = from;
        }

        /// <summary>
        /// Gets or sets the data being loaded.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the source to load from.
        /// </summary>
        public TFrom From { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "LOAD";

        /// <summary>
        /// Gets the action function that performs the load operation.
        /// </summary>
        public abstract Func<TFrom, TWhat> Act { get; }

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
        /// For LOAD verbs, expects either a FROM keyword or a WHAT noun.
        /// </summary>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is IFrom<TFrom>)
            {
                return ValidationResult.Success();
            }

            return nextWord is IWhat<TWhat>
                ? ValidationResult.Success()
                : ValidationResult.Failure(
                    "Invalid word after LOAD verb. Expected FROM keyword or a direct object.");
        }

        /// <summary>
        /// Executes the LOAD operation and returns the loaded data.
        /// </summary>
        /// <returns>The data that was loaded</returns>
        public virtual TWhat Execute()
        {
            return Act(From);
        }

        /// <summary>
        /// Creates a THEN chain that passes the loaded data to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the loaded data</returns>
        public virtual IThen<TWhat> Then()
        {
            var result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}
