namespace FluNET.Matching
{
    /// <summary>
    /// Service that resolves the appropriate matcher implementation based on configuration.
    /// Allows switching between regex-based and string-based matchers for performance tuning.
    /// </summary>
    public class MatcherResolver
    {
        private readonly IEnumerable<IMatcher> _matchers;
        private readonly bool _useRegex;

        /// <summary>
        /// Creates a new MatcherResolver instance.
        /// </summary>
        /// <param name="matchers">Collection of all registered matchers</param>
        /// <param name="useRegex">If true, uses regex-based matchers; if false, uses string-based matchers for better performance. Default is false (string-based).</param>
        public MatcherResolver(IEnumerable<IMatcher> matchers, bool useRegex = false)
        {
            _matchers = matchers ?? throw new ArgumentNullException(nameof(matchers));
            _useRegex = useRegex;
        }

        /// <summary>
        /// Gets the appropriate matcher implementation for the specified type.
        /// Uses configuration to determine whether to use regex or string-based implementation.
        /// </summary>
        /// <typeparam name="T">The matcher interface type (IVariableMatcher, IReferenceMatcher, or IDestructuringMatcher)</typeparam>
        /// <returns>The configured matcher implementation</returns>
        /// <exception cref="InvalidOperationException">Thrown when no matching implementation is found</exception>
        public T GetMatcher<T>() where T : IMatcher
        {
            var matcherType = typeof(T);

            // Get all matchers that match the requested type
            var matchersOfType = _matchers
                .Where(m => m.MatcherType == matcherType)
                .ToList();

            if (!matchersOfType.Any())
            {
                throw new InvalidOperationException(
                    $"No matchers found for type {matcherType.Name}. " +
                    "Ensure matcher implementations are registered in DI.");
            }

            // Select based on configuration
            IMatcher? selectedMatcher;
            if (_useRegex)
            {
                selectedMatcher = matchersOfType.FirstOrDefault(m => m.GetType().Name.Contains("Regex"));
            }
            else
            {
                selectedMatcher = matchersOfType.FirstOrDefault(m => m.GetType().Name.Contains("String"));
            }

            if (selectedMatcher == null)
            {
                throw new InvalidOperationException(
                    $"No {(_useRegex ? "regex" : "string")} implementation found for {matcherType.Name}. " +
                    $"UseRegex configuration is set to: {_useRegex}");
            }

            return (T)selectedMatcher;
        }
    }
}
