namespace FluNET.Matching.StringBased
{
    /// <summary>
    /// String-based implementation for matching reference patterns like {reference}.
    /// Uses manual string parsing instead of regex for better performance.
    /// </summary>
    public class StringReferenceMatcher : IReferenceMatcher
    {
        public Type MatcherType => typeof(IReferenceMatcher);

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

            // Find first '{' and last '}'
            int startIndex = input.IndexOf('{');
            int endIndex = input.LastIndexOf('}');

            if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
                return string.Empty;

            // Extract content between braces
            return input.Substring(startIndex + 1, endIndex - startIndex - 1);
        }
    }
}
