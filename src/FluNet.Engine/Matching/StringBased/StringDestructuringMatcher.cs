namespace FluNET.Matching.StringBased
{
    /// <summary>
    /// String-based implementation for matching destructuring patterns like [{prop1, prop2}].
    /// Uses manual string parsing instead of regex for better performance.
    /// </summary>
    public class StringDestructuringMatcher : IDestructuringMatcher
    {
        public Type MatcherType => typeof(IDestructuringMatcher);

        public bool IsMatch(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.StartsWith('{') && input.EndsWith('}') && input.Length > 2;
        }

        public string Extract(string input)
        {
            if (!IsMatch(input))
                return string.Empty;

            // Extract content between { and }
            return input.Substring(1, input.Length - 2);
        }

        public string[] GetPropertyNames(string input)
        {
            var extracted = Extract(input);
            if (string.IsNullOrEmpty(extracted))
                return Array.Empty<string>();

            // Split by comma and trim whitespace
            return extracted
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();
        }
    }
}
