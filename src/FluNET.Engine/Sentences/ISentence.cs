<<<<<<< HEAD
using FluNET.Syntax.Core;
=======
﻿using FluNET.Syntax.Core;
>>>>>>> origin/agent/stabilize-poc-foundation

namespace FluNET.Sentences
{
    /// <summary>
<<<<<<< HEAD
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
=======
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
>>>>>>> origin/agent/stabilize-poc-foundation
