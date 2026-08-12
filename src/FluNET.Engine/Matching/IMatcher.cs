namespace FluNET.Matching
{
    /// <summary>
    /// Base interface for pattern matchers that can identify and extract patterns from strings.
    /// Implementations can use regex or string-based parsing approaches.
    /// </summary>
    public interface IMatcher
    {
        /// <summary>
        /// Gets the type of matcher this implementation represents.
        /// Used for resolving specific matcher implementations from a collection.
        /// </summary>
        Type MatcherType { get; }

        /// <summary>
        /// Determines if the input string matches the expected pattern.
        /// </summary>
        /// <param name="input">The input string to test</param>
        /// <returns>True if the input matches the pattern, false otherwise</returns>
        bool IsMatch(string input);

        /// <summary>
        /// Extracts the content from the matched pattern.
        /// For example, "[variable]" would extract "variable".
        /// </summary>
        /// <param name="input">The input string to extract from</param>
        /// <returns>The extracted content, or empty string if no match</returns>
        string Extract(string input);
    }

    /// <summary>
    /// Marker interface for variable matchers that identify [variable] patterns.
    /// </summary>
    public interface IVariableMatcher : IMatcher { }

    /// <summary>
    /// Marker interface for reference matchers that identify {reference} patterns.
    /// </summary>
    public interface IReferenceMatcher : IMatcher { }

    /// <summary>
    /// Specialized matcher for destructuring patterns like [{prop1, prop2, prop3}].
    /// Extracts individual property names from the pattern.
    /// </summary>
    public interface IDestructuringMatcher : IMatcher
    {
        /// <summary>
        /// Gets the individual property names from a destructuring pattern.
        /// </summary>
        /// <param name="input">The destructuring pattern (e.g., "[{name, age}]")</param>
        /// <returns>Array of property names, or empty array if no match</returns>
        string[] GetPropertyNames(string input);
    }
}
