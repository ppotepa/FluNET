using FluNET.Keywords;

namespace FluNET.Syntax.Core
{
    /// <summary>
    /// Non-generic verb interface for actions that don't require type parameters.
    /// Verbs are action words that can validate subsequent words in a sentence.
    /// </summary>
    public interface IVerb : IWord, IKeyword
    {
        /// <summary>
        /// Gets the synonyms for this verb.
        /// These alternative keywords have exactly the same implementation as the main verb.
        /// </summary>
        string[] Synonyms => Array.Empty<string>();
    }

    /// <summary>
    /// Generic verb interface for type-safe actions.
    /// </summary>
    /// <typeparam name="TWhat">The type of object being acted upon (direct object)</typeparam>
    /// <typeparam name="TFrom">The type of the source/origin from which the action retrieves data</typeparam>
    public interface IVerb<TWhat, TFrom> : IVerb
    {
        /// <summary>
        /// The action function that takes a source and produces a result.
        /// Example: For GET verb, Act takes a FileInfo and returns string content.
        /// </summary>
        public Func<TFrom, TWhat> Act { get; }

        /// <summary>
        /// Invokes the verb's action and returns the result.
        /// This is the primary execution method that should be called to run the verb.
        /// </summary>
        /// <returns>The result of the verb's action</returns>
        TWhat Invoke();

        /// <summary>
        /// Resolves a string value to the TFrom type contextually.
        /// This allows each verb implementation to define how to interpret the value after prepositions.
        /// For example: file.txt → FileInfo, https://... → Uri, etc.
        /// This is the extensibility point for plugin verbs.
        /// </summary>
        /// <param name="value">The string value to resolve</param>
        /// <returns>The resolved TFrom instance, or null if resolution fails</returns>
        TFrom? Resolve(string value);
    }
}