namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for POST verbs that send data to a destination.
    /// The POST verb follows the pattern: POST [data] TO [destination]
    /// </summary>
    /// <typeparam name="TWhat">The type of data being sent (e.g., string, byte[], object)</typeparam>
    /// <typeparam name="TTo">The type of the destination receiving data (e.g., Uri, FileInfo)</typeparam>
    public abstract class Post<TWhat, TTo> : IVerb<TWhat, TTo>,
        IWhat<TWhat>,
        ITo<TTo>
    {
        /// <summary>
        /// Initializes a new instance of the Post verb.
        /// </summary>
        /// <param name="what">The data being sent</param>
        /// <param name="to">The destination to send data to</param>
        protected Post(TWhat what, TTo to)
        {
            What = what;
            To = to;
        }

        /// <summary>
        /// Gets or sets the data being sent.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the destination to send data to.
        /// </summary>
        public TTo To { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "POST";

        /// <summary>
        /// Gets the action function that performs the POST operation.
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
        /// For POST verbs, expects either a TO keyword or a WHAT noun.
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
                    "Invalid word after POST verb. Expected TO keyword or a direct object.");
        }

        /// <summary>
        /// Executes the POST operation and returns the sent data.
        /// </summary>
        /// <returns>The data that was posted</returns>
        public virtual TWhat Execute()
        {
            return Act(To);
        }

        /// <summary>
        /// Creates a THEN chain that passes the posted data to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the posted data</returns>
        public virtual IThen<TWhat> Then()
        {
            var result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}
