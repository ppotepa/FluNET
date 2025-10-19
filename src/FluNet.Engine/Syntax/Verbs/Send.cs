namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for SEND verbs that transmit data to a recipient.
    /// The SEND verb follows the pattern: SEND [message] TO [recipient]
    /// </summary>
    /// <typeparam name="TWhat">The type of message being sent</typeparam>
    /// <typeparam name="TTo">The type of the recipient</typeparam>
    public abstract class Send<TWhat, TTo> : IVerb<TWhat, TTo>,
        IWhat<TWhat>,
        ITo<TTo>
    {
        /// <summary>
        /// Initializes a new instance of the Send verb.
        /// </summary>
        /// <param name="what">The message being sent</param>
        /// <param name="to">The recipient</param>
        protected Send(TWhat what, TTo to)
        {
            What = what;
            To = to;
        }

        /// <summary>
        /// Gets or sets the message being sent.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the recipient.
        /// </summary>
        public TTo To { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "SEND";

        /// <summary>
        /// Gets the action function that performs the send operation.
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
        /// For SEND verbs, expects either a TO keyword or a WHAT noun.
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
                    "Invalid word after SEND verb. Expected TO keyword or a direct object.");
        }

        /// <summary>
        /// Executes the SEND operation and returns the sent message.
        /// </summary>
        /// <returns>The message that was sent</returns>
        public virtual TWhat Execute()
        {
            return Act(To);
        }

        /// <summary>
        /// Creates a THEN chain that passes the sent message to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the sent message</returns>
        public virtual IThen<TWhat> Then()
        {
            var result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}
