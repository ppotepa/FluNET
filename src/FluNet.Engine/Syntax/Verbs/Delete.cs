namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for DELETE verbs that remove data from a location.
    /// The DELETE verb follows the pattern: DELETE [resource] FROM [location]
    /// </summary>
    /// <typeparam name="TWhat">The type of resource being deleted (e.g., string path, identifier)</typeparam>
    /// <typeparam name="TFrom">The type of the location (e.g., FileInfo, DatabaseContext)</typeparam>
    public abstract class Delete<TWhat, TFrom> : IVerb<TWhat, TFrom>,
        IWhat<TWhat>,
        IFrom<TFrom>
    {
        /// <summary>
        /// Initializes a new instance of the Delete verb.
        /// </summary>
        /// <param name="what">The resource being deleted</param>
        /// <param name="from">The location to delete from</param>
        protected Delete(TWhat what, TFrom from)
        {
            What = what;
            From = from;
        }

        /// <summary>
        /// Gets or sets the resource being deleted.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the location to delete from.
        /// </summary>
        public TFrom From { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "DELETE";

        /// <summary>
        /// Gets the action function that performs the delete operation.
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
        /// For DELETE verbs, expects either a FROM keyword or a WHAT noun.
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
                    "Invalid word after DELETE verb. Expected FROM keyword or a direct object.");
        }

        /// <summary>
        /// Executes the DELETE operation and returns the deleted resource identifier.
        /// </summary>
        /// <returns>The identifier of the deleted resource</returns>
        public virtual TWhat Execute()
        {
            return Act(From);
        }

        /// <summary>
        /// Creates a THEN chain that passes the deleted resource identifier to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the deleted resource identifier</returns>
        public virtual IThen<TWhat> Then()
        {
            var result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}
