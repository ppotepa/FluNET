using FluNET.Keywords;

namespace FluNET.Syntax
{
    /// <summary>
    /// Non-generic verb interface for actions that don't require type parameters.
    /// Verbs are action words that can validate subsequent words in a sentence.
    /// </summary>
    public interface IVerb : IWord, IKeyword, IValidatable
    {
        /// <summary>
        /// The action function to execute when this verb is processed.
        /// </summary>
        public Func<object> Act { get; }
    }

    /// <summary>
    /// Generic verb interface for type-safe actions.
    /// </summary>
    /// <typeparam name="TWhat">The type of object being acted upon (direct object)</typeparam>
    /// <typeparam name="TFrom">The type of the source/origin from which the action retrieves data</typeparam>
    public interface IVerb<TWhat, TFrom> : IWord, IKeyword, IValidatable
    {
        /// <summary>
        /// The action function that takes a source and produces a result.
        /// </summary>
        public Func<TFrom, TWhat> Act { get; }
    }
}