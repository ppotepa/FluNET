using System.Text.RegularExpressions;

namespace FluNET.Matching.Regex
{
    /// <summary>
    /// Regex-based implementation for matching destructuring patterns like [{prop1, prop2}].
    /// Pattern: ^\{(.+)\}$ for extraction of property list.
    /// </summary>
    public class RegexDestructuringMatcher : IDestructuringMatcher
    {
        private static readonly System.Text.RegularExpressions.Regex DestructuringPattern = new(@"^\{(.+)\}$", RegexOptions.Compiled);

        public Type MatcherType => typeof(IDestructuringMatcher);

        public bool IsMatch(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return input.StartsWith('{') && input.EndsWith('}');
        }

        public string Extract(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var match = DestructuringPattern.Match(input);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public string[] GetPropertyNames(string input)
        {
            var extracted = Extract(input);
            if (string.IsNullOrEmpty(extracted))
                return Array.Empty<string>();

            return extracted
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToArray();
        }
    }
}
