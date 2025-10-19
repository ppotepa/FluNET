using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;

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
        /// Validates whether this verb implementation can handle the given word.
        /// Derived classes must implement this to validate parameters like USING instruments.
        /// </summary>
        public abstract bool Validate(IWord word);

        /// <summary>
        /// Resolves a string value to the TUsing type contextually.
        /// Derived classes implement this to define resolution logic.
        /// </summary>
        public abstract TUsing? Resolve(string value);

        /// <summary>
        /// Determines if this verb can handle the given sentence structure.
        /// Checks for the USING preposition and validates its value.
        /// </summary>
        /// <param name="root">The root word of the sentence</param>
        /// <returns>True if this verb can handle the sentence structure</returns>
        public virtual bool CanHandle(IWord root)
        {
            // Find the USING keyword
            Keywords.Using? usingPrep = root.Find<Keywords.Using>();
            if (usingPrep == null)
            {
                return false;
            }

            // The USING preposition should have a value after it
            IWord? valueWord = usingPrep.Next;
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
        /// For TRANSFORM verbs, expects either a USING keyword, a WHAT noun, or a variable.
        /// </summary>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is Keywords.Using)
            {
                return ValidationResult.Success();
            }

            // Check if it's a variable word (will be resolved during execution)
            return nextWord is Words.VariableWord
                ? ValidationResult.Success()
                : nextWord is IWhat<TWhat>
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
            TWhat? result = Execute();
            return new ThenKeyword<TWhat>(result);
        }
    }
}