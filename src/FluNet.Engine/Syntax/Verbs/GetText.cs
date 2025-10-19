using FluNET.Syntax.Core;
using FluNET.Words;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of the GET verb for retrieving text from a file.
    /// Usage: GET [data] FROM [file.txt]
    /// </summary>
    public class GetText : Get<string[], FileInfo>
    {
        /// <summary>
        /// Initializes a new instance of GetText.
        /// </summary>
        /// <param name="what">The text data to be retrieved (array of lines)</param>
        /// <param name="from">The file from which to read text</param>
        public GetText(string[] what, FileInfo from) : base(what, from)
        {
        }

        /// <summary>
        /// Gets the action function that reads all text from the file and splits it into lines.
        /// </summary>
        public override Func<FileInfo, string[]> Act
        {
            get
            {
                return (info) =>
                {
                    using (StreamReader reader = new(info.OpenRead()))
                    {
                        return reader.ReadToEnd().Split('\n');
                    }
                };
            }
        }

        /// <summary>
        /// Validates that the word represents a valid file path or FileInfo that exists.
        /// </summary>
        public override bool Validate(IWord word)
        {
            // For literal words (like file paths), just check if it's a valid format
            // File existence will be checked during execution
            if (word is LiteralWord literalWord)
            {
                // Accept any non-empty literal as a potential file path
                return !string.IsNullOrWhiteSpace(literalWord.Value.TrimEnd('.'));
            }

            // For variable words, we'll need to resolve them at execution time
            if (word is VariableWord)
            {
                return true;
            }

            // For reference words {file.txt}, we validate that they're valid file paths
            return word is ReferenceWord referenceWord ? !string.IsNullOrWhiteSpace(referenceWord.Reference) : false;
        }

        /// <summary>
        /// Resolves a string value or ReferenceWord to FileInfo.
        /// This allows GET [text] FROM {file.txt} to work contextually.
        /// </summary>
        /// <param name="value">The file path string or reference</param>
        /// <returns>FileInfo instance, or null if invalid</returns>
        public override FileInfo? Resolve(string value)
        {
            // Return null for empty or whitespace strings
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                // FileInfo handles both absolute and relative paths
                return new FileInfo(value);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a ReferenceWord to FileInfo.
        /// </summary>
        public FileInfo? Resolve(ReferenceWord reference)
        {
            return reference.ResolveAs<FileInfo>();
        }
    }
}