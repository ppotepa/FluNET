namespace FluNET.Matching.StringBased
{
    /// <summary>
    /// String-based implementation for matching variable patterns like [variable].
    /// Uses manual string parsing instead of regex for better performance.
    /// </summary>
    public class StringVariableMatcher : IVariableMatcher
    {
        public Type MatcherType => typeof(IVariableMatcher);

        public bool IsMatch(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.StartsWith('[') && input.EndsWith(']') && input.Length > 2;
        }

        public string Extract(string input)
        {
            if (!IsMatch(input))
                return string.Empty;

            // Find first '[' and last ']'
            int startIndex = input.IndexOf('[');
            int endIndex = input.LastIndexOf(']');

            if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
                return string.Empty;

            // Extract content between brackets
            return input.Substring(startIndex + 1, endIndex - startIndex - 1);
        }
    }
}
