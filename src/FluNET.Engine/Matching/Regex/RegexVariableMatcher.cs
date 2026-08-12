using System.Text.RegularExpressions;

namespace FluNET.Matching.Regex
{
    /// <summary>
    /// Regex-based implementation for matching variable patterns like [variable].
    /// Pattern: ^\[.+\]$ for detection, \[([^\]]+)\] for extraction.
    /// </summary>
    public class RegexVariableMatcher : IVariableMatcher
    {
        private static readonly System.Text.RegularExpressions.Regex DetectionPattern = new(@"^\[.+\]$", RegexOptions.Compiled);
        private static readonly System.Text.RegularExpressions.Regex ExtractionPattern = new(@"\[([^\]]+)\]", RegexOptions.Compiled);

        public Type MatcherType => typeof(IVariableMatcher);

        public bool IsMatch(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return DetectionPattern.IsMatch(input);
        }

        public string Extract(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var match = ExtractionPattern.Match(input);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
