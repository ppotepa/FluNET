using FluNET.Syntax;

namespace FluNET.Sentences
{
    /// <summary>
    /// Represents a validated sentence composed of a chain of words
    /// </summary>
    public interface ISentence
    {
        /// <summary>
        /// The first word in the sentence (typically a verb)
        /// </summary>
        IWord? Root { get; }
    }
}