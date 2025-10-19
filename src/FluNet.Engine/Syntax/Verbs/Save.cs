namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for SAVE verbs that store data to a destination.
    /// The SAVE verb follows the pattern: SAVE [data] TO [destination]
    /// </summary>
    /// <typeparam name="TWhat">The type of data being saved (e.g., string, byte[], object)</typeparam>
    /// <typeparam name="TTo">The type of the destination (e.g., FileInfo, DatabaseRecord)</typeparam>
    public abstract class Save<TWhat, TTo> : IVerb<TWhat, TTo>,
        IWhat<TWhat>,
        ITo<TTo>
    {
        /// <summary>
        /// Initializes a new instance of the Save verb.
        /// </summary>
        /// <param name="what">The data being saved</param>
        /// <param name="to">The destination where data is saved</param>
        protected Save(TWhat what, TTo to)
        {
            What = what;
            To = to;
        }

        /// <summary>
        /// Gets or sets the data being saved.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the destination where data is saved.
        /// </summary>
        public TTo To { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "SAVE";

        /// <summary>
        /// Gets the action function that performs the save operation.
        /// </summary>
        public abstract Func<TTo, TWhat> Act { get; }

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
        /// For SAVE verbs, expects either a TO keyword or a WHAT noun.
        /// </summary>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is ITo<TTo>)
            {
                return ValidationResult.Success();
            }

            return nextWord is IWhat<TWhat>
                ? ValidationResult.Success()
                : ValidationResult.Failure(
                    "Invalid word after SAVE verb. Expected TO keyword or a direct object.");
        }

        /// <summary>
        /// Executes the SAVE operation and returns the saved data.
        /// </summary>
        /// <returns>The data that was saved</returns>
        public virtual TWhat Execute()
        {
            return Act(To);
        }

        /// <summary>
        /// Creates a THEN chain that passes the saved data to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the saved data</returns>
        public virtual IThen<TWhat> Then()
        {
            var result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}
