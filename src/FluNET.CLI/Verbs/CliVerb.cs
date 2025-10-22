using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Validation;
using FluNET.Words;

namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// Abstract base class for simple CLI verbs that operate on a subject (what).
    /// Pattern: VERB [what]
    /// Example: CLEAR SCREEN, SHOW HISTORY
    /// </summary>
    /// <typeparam name="TWhat">The type of the subject being acted upon</typeparam>
    public abstract class CliVerb<TWhat> : IVerb, IWord, IKeyword, IWhat<TWhat>
    {
        /// <summary>
        /// Initializes a new instance of the CLI verb.
        /// </summary>
        /// <param name="what">The subject being acted upon</param>
        protected CliVerb(TWhat what)
        {
            What = what;
        }

        /// <summary>
        /// Gets the subject being acted upon.
        /// </summary>
        public TWhat What { get; protected set; }

        /// <summary>
        /// Gets the keyword text for this verb.
        /// </summary>
        public abstract string Text { get; }

        /// <summary>
        /// Gets the synonyms for this verb.
        /// These alternative keywords can be used as standalone aliases.
        /// </summary>
        public abstract string[] Synonyms { get; }

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
        /// For CLI verbs, the next word should be the subject (what) or terminator.
        /// </summary>
        /// <param name="nextWord">The word following this verb</param>
        /// <param name="lexicon">The lexicon for advanced validation</param>
        /// <returns>ValidationResult indicating if the next word is valid</returns>
        public virtual ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
        {
            if (nextWord is LiteralWord literal)
            {
                string value = literal.Value.TrimEnd('.').ToUpperInvariant();

                // Check if this is a valid subject for this verb
                if (IsValidSubject(value))
                {
                    return ValidationResult.Success();
                }

                // Accept terminator (for alias usage: just the verb name)
                if (value == "." || string.IsNullOrWhiteSpace(value))
                {
                    return ValidationResult.Success();
                }

                return ValidationResult.Failure($"{Text} must be followed by a valid subject or be standalone.");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates whether the given word is acceptable for this verb.
        /// </summary>
        public virtual bool Validate(IWord word)
        {
            return true;
        }

        /// <summary>
        /// Determines if the given value is a valid subject for this verb.
        /// Override this to specify what subjects are valid (e.g., "SCREEN" for CLEAR).
        /// </summary>
        /// <param name="subject">The subject value to validate</param>
        /// <returns>True if the subject is valid for this verb</returns>
        protected abstract bool IsValidSubject(string subject);

        /// <summary>
        /// Executes the CLI verb operation.
        /// </summary>
        public abstract void Execute();
    }
}