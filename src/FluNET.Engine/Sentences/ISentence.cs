using FluNET.Syntax.Core;

namespace FluNET.Sentences
{
    /// <summary>
    /// Represents a validated sentence composed of a chain of words.
    /// Can contain sub-sentences connected by THEN keyword.
    /// </summary>
    public interface ISentence
    {
        /// <summary>
        /// The first word in the sentence (typically a verb)
        /// </summary>
        IWord? Root { get; }

        /// <summary>
        /// Sub-sentences chained with THEN keyword.
        /// These are executed sequentially in the same variable context.
        /// </summary>
        IList<ISentence> SubSentences { get; }

        /// <summary>
        /// Indicates if this sentence has chained sub-sentences.
        /// </summary>
        bool HasSubSentences { get; }
    }
}