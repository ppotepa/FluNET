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
        /// Validates whether this verb implementation can handle the given word.
        /// Derived classes must implement this to validate parameters like TO destinations.
        /// </summary>
        public abstract bool Validate(IWord word);

        /// <summary>
        /// Resolves a string value to the TTo type contextually.
        /// Derived classes implement this to define resolution logic.
        /// </summary>
        public abstract TTo? Resolve(string value);

        /// <summary>
        /// Determines if this verb can handle the given sentence structure.
        /// Checks for the TO preposition and validates its value.
        /// </summary>
        /// <param name="root">The root word of the sentence</param>
        /// <returns>True if this verb can handle the sentence structure</returns>
        public virtual bool CanHandle(IWord root)
        {
            // Find the TO keyword
            Keywords.To? toPrep = root.Find<Keywords.To>();
            if (toPrep == null)
            {
                return false;
            }

            // The TO preposition should have a value after it
            IWord? valueWord = toPrep.Next;
            if (valueWord == null)
            {
                return false;
            }

            // Validate the value using the derived class's validation logic
            return Validate(valueWord);
        }

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
        /// For SEND verbs, expects either a TO keyword, a WHAT noun, or a variable.
        /// </summary>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is Keywords.To)
            {
                return ValidationResult.Success();
            }

            // Check if it's a variable word (will be resolved during execution)
            return nextWord is Words.VariableWord
                ? ValidationResult.Success()
                : nextWord is IWhat<TWhat>
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
            TWhat? result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}
