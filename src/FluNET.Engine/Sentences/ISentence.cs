using FluNET.Syntax.Core;

namespace FluNET.Sentences
{
    /// <summary>
    /// Legacy compatibility representation of a validated word chain. Canonical
    /// compilation and execution use BoundProgram and ExecutionPlan instead.
    /// </summary>
    [Obsolete("ISentence is a legacy compatibility view. Use CompilationResult/BoundProgram/ExecutionPlan and Engine.ExecuteAsync instead.")]
    public interface ISentence
    {
        /// <summary>The first legacy word in the sentence.</summary>
        IWord? Root { get; }

        /// <summary>Legacy sequential sub-sentences.</summary>
        IList<ISentence> SubSentences { get; }

        /// <summary>Indicates whether the compatibility view contains sub-sentences.</summary>
        bool HasSubSentences { get; }
    }
}
