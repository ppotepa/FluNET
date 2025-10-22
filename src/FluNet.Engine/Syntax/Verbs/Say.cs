using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Abstract base class for SAY verbs that output text to the console.
    /// The SAY verb follows the pattern: SAY [what]
    /// where [what] is the text/message to display.
    /// </summary>
    /// <typeparam name="TWhat">The type of data being output (typically string)</typeparam>
    public abstract class Say<TWhat> : IVerb, IWord, IKeyword,
        IWhat<TWhat>
    {
        /// <summary>
        /// Initializes a new instance of the Say verb.
        /// </summary>
        /// <param name="what">The text/message to output</param>
        protected Say(TWhat what)
        {
            this.What = what;
        }

        /// <summary>
        /// Gets or sets the text/message being output.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public string Text => "SAY";

        /// <summary>
        /// Gets the synonyms for this verb.
        /// These alternative keywords have exactly the same implementation as the main verb.
        /// </summary>
        public virtual string[] Synonyms => new[] { "ECHO", "PRINT", "OUTPUT", "WRITE" };

        /// <summary>
        /// Gets the action function that performs the output operation.
        /// </summary>
        public abstract Func<TWhat, TWhat> Act { get; }

        /// <summary>
        /// Validates whether this verb implementation can handle the given word.
        /// </summary>
        public abstract bool Validate(IWord word);

        /// <summary>
        /// Determines if this verb can handle the given sentence structure.
        /// SAY just needs [what] - no FROM or TO required.
        /// </summary>
        /// <param name="root">The root word of the sentence</param>
        /// <returns>True if this verb can handle the sentence structure</returns>
        public virtual bool CanHandle(IWord root)
        {
            // SAY just needs the [what] parameter, which should be right after the verb
            IWord? valueWord = root.Next;
            if (valueWord == null)
            {
                return false;
            }

            // Validate the value using the derived class's validation logic
            return Validate(valueWord);
        }

        // IWord navigation properties
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
        /// For SAY verbs, the word after SAY should be [what] to output.
        /// This can be:
        /// - A literal value (SAY Hello World)
        /// - A variable reference (SAY [message])
        /// - A reference (SAY {filepath})
        /// </summary>
        /// <param name="nextWord">The word following this verb</param>
        /// <param name="lexicon">The lexicon for advanced validation</param>
        /// <returns>ValidationResult indicating if the next word is valid</returns>
        public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            // SAY must be followed by [what] - the message to output
            // Accept variable, reference, literal, or IWhat noun as the [what] parameter
            bool isValidWhat = nextWord is Words.LiteralWord
                            || nextWord is Words.VariableWord
                            || nextWord is Words.ReferenceWord
                            || nextWord is IWhat<TWhat>;

            if (!isValidWhat)
            {
                return ValidationResult.Failure(
                    $"Invalid word after SAY verb. Expected literal text, [variable], or {{reference}} specifying what to say.");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Invokes the SAY operation and returns the output message.
        /// </summary>
        /// <returns>The message that was output</returns>
        public virtual TWhat Invoke()
        {
            return Act(What);
        }

    }
}
