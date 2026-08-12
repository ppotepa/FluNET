using FluNET.Syntax.Core;

namespace FluNET.Syntax.Validation
{
    /// <summary>
    /// Interface for words that can validate what type of word should follow them in a sentence.
    /// Typically implemented by verbs and prepositions to enforce correct grammatical structure.
    /// </summary>
    public interface IValidatable
    {
        /// <summary>
        /// Validates whether the next word in the sentence is grammatically valid following this word.
        /// </summary>
        /// <param name="nextWord">The word that follows this one in the sentence</param>
        /// <param name="lexicon">The lexicon containing verb usage information for advanced validation</param>
        /// <returns>A ValidationResult indicating success or failure with an error message</returns>
        ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon);

        /// <summary>
        /// Validates whether this keyword implementation can handle the given word value.
        /// For example, a FROM keyword in a file-based verb can check if the file exists.
        /// </summary>
        /// <param name="word">The word to validate</param>
        /// <returns>True if this implementation can handle the word, false otherwise</returns>
        bool Validate(IWord word);
    }
}