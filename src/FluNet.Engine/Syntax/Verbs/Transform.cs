namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for TRANSFORM verbs that convert data using a method or algorithm.
    /// The TRANSFORM verb follows the pattern: TRANSFORM [data] USING [method]
    /// </summary>
    /// <typeparam name="TWhat">The type of data being transformed</typeparam>
    /// <typeparam name="TUsing">The type of method/algorithm used for transformation</typeparam>
    public abstract class Transform<TWhat, TUsing> : IVerb<TWhat, TUsing>,
        IWhat<TWhat>,
        IUsing<TUsing>
    {
        /// <summary>
        /// Initializes a new instance of the Transform verb.
        /// </summary>
        /// <param name="what">The data being transformed</param>
        /// <param name="using">The method/algorithm to use</param>
        protected Transform(TWhat what, TUsing @using)
        {
            What = what;
            Using = @using;
        }

        /// <summary>
        /// Gets or sets the data being transformed.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the method/algorithm used for transformation.
        /// </summary>
        public TUsing Using { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "TRANSFORM";

        /// <summary>
        /// Gets the action function that performs the transformation.
        /// </summary>
        public abstract Func<TUsing, TWhat> Act { get; }

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
        /// For TRANSFORM verbs, expects either a USING keyword or a WHAT noun.
        /// </summary>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is IUsing<TUsing>)
            {
                return ValidationResult.Success();
            }

            return nextWord is IWhat<TWhat>
                ? ValidationResult.Success()
                : ValidationResult.Failure(
                    "Invalid word after TRANSFORM verb. Expected USING keyword or a direct object.");
        }

        /// <summary>
        /// Executes the TRANSFORM operation and returns the transformed data.
        /// </summary>
        /// <returns>The transformed data</returns>
        public virtual TWhat Execute()
        {
            return Act(Using);
        }

        /// <summary>
        /// Creates a THEN chain that passes the transformed data to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the transformed data</returns>
        public virtual IThen<TWhat> Then()
        {
            var result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}
