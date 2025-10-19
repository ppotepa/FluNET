namespace FluNET.Syntax
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
    }
}
