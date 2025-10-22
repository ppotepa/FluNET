using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;

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
        /// Validates whether this verb implementation can handle the given word.
        /// Derived classes must implement this to validate parameters like TO destinations.
        /// </summary>
        public abstract bool Validate(IWord word);

        /// <summary>
        /// Resolves a string value to the TTo type contextually.
        /// Derived classes implement this to define resolution logic (e.g., file path → FileInfo).
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
        /// For SAVE verbs, expects either a TO keyword, a WHAT noun, a variable, or a literal value.
        /// </summary>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is Keywords.To)
            {
                return ValidationResult.Success();
            }

            // Check if it's a variable word, literal word, or reference word (will be resolved during execution)
            return nextWord is Words.VariableWord
                || nextWord is Words.LiteralWord
                || nextWord is Words.ReferenceWord
                || nextWord is IWhat<TWhat>
                    ? ValidationResult.Success()
                    : ValidationResult.Failure(
                        "Invalid word after SAVE verb. Expected TO keyword or a direct object.");
        }

        /// <summary>
        /// Invokes the SAVE operation and returns the saved data.
        /// </summary>
        /// <returns>The data that was saved</returns>
        public virtual TWhat Invoke()
        {
            return Act(To);
        }
    }
}