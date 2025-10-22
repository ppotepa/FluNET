using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for DELETE verbs that remove data from a location.
    /// The DELETE verb follows the pattern: DELETE [resource] FROM [location]
    /// The FROM parameter is optional - if not provided, uses a default location.
    /// </summary>
    /// <typeparam name="TWhat">The type of resource being deleted (e.g., string path, identifier)</typeparam>
    /// <typeparam name="TFrom">The type of the location (e.g., FileInfo, DatabaseContext) - nullable to indicate FROM is optional</typeparam>
    public abstract class Delete<TWhat, TFrom> : IVerb<TWhat, TFrom>,
        IWhat<TWhat>,
        IFrom<TFrom?>
    {
        /// <summary>
        /// Initializes a new instance of the Delete verb.
        /// </summary>
        /// <param name="what">The resource being deleted</param>
        /// <param name="from">The location to delete from (optional - can be null)</param>
        protected Delete(TWhat what, TFrom? from)
        {
            What = what;
            From = from;
        }

        /// <summary>
        /// Gets or sets the resource being deleted.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets or sets the location to delete from (optional).
        /// </summary>
        public TFrom? From { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "DELETE";

        /// <summary>
        /// Gets the action function that performs the delete operation.
        /// The function receives a nullable TFrom parameter.
        /// </summary>
        public abstract Func<TFrom?, TWhat> Act { get; }

        /// <summary>
        /// Validates whether this verb implementation can handle the given word.
        /// Derived classes must implement this to validate parameters like FROM sources.
        /// </summary>
        public abstract bool Validate(IWord word);

        /// <summary>
        /// Resolves a string value to the TFrom type contextually.
        /// Derived classes implement this to define resolution logic.
        /// </summary>
        public abstract TFrom? Resolve(string value);

        /// <summary>
        /// Determines if this verb can handle the given sentence structure.
        /// Checks for the FROM preposition and validates its value.
        /// If FROM is not present, validates the word after the verb as a direct object.
        /// </summary>
        /// <param name="root">The root word of the sentence</param>
        /// <returns>True if this verb can handle the sentence structure</returns>
        public virtual bool CanHandle(IWord root)
        {
            // Find the FROM keyword
            Keywords.From? fromPrep = root.Find<Keywords.From>();

            if (fromPrep != null)
            {
                // FROM pattern: DELETE [filename] FROM [directory]
                // The FROM preposition should have a value after it
                IWord? valueWord = fromPrep.Next;
                if (valueWord == null)
                {
                    return false;
                }

                // Validate the value using the derived class's validation logic
                return Validate(valueWord);
            }

            // No FROM - direct object pattern: DELETE [filepath]
            // Check if there's a word after the verb (the filepath)
            IWord? directObject = root.Next;
            if (directObject == null)
            {
                return false;
            }

            // Validate the direct object
            return Validate(directObject);
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
        /// For DELETE verbs, expects either a FROM keyword, a WHAT noun, a variable, or a literal value.
        /// </summary>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is Keywords.From)
            {
                return ValidationResult.Success();
            }

            // Check if it's a variable word (will be resolved during execution)
            // Or a literal word (e.g., DELETE file.txt)
            // Or a reference word (e.g., DELETE {filepath})
            return nextWord is Words.VariableWord
                || nextWord is Words.LiteralWord
                || nextWord is Words.ReferenceWord
                || nextWord is IWhat<TWhat>
                    ? ValidationResult.Success()
                    : ValidationResult.Failure(
                        "Invalid word after DELETE verb. Expected FROM keyword or a direct object.");
        }

        /// <summary>
        /// Invokes the DELETE operation and returns the deleted resource identifier.
        /// </summary>
        /// <returns>The identifier of the deleted resource</returns>
        public virtual TWhat Invoke()
        {
            return Act(From);
        }

        /// <summary>
        /// Creates a THEN chain that passes the deleted resource identifier to the next operation.
        /// </summary>
        /// <returns>A THEN keyword with the deleted resource identifier</returns>
        public virtual IThen<TWhat> Then()
        {
            TWhat? result = Invoke();
            return new ThenKeyword<TWhat>(result);
        }
    }
}