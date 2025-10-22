using FluNET.Syntax.Core;
using FluNET.Words;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of LOAD verb for loading text from files.
    /// Usage: LOAD [text] FROM {file.txt}
    /// Similar to GET but emphasizes loading into memory/state.
    /// </summary>
    public class LoadText : Load<string[], FileInfo>
    {
        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public LoadText() : base(Array.Empty<string>(), new FileInfo("temp"))
        {
        }

        /// <summary>
        /// Initializes a new instance of LoadText.
        /// </summary>
        /// <param name="what">The text lines to load</param>
        /// <param name="from">The file to load from</param>
        public LoadText(string[] what, FileInfo from) : base(what, from)
        {
        }

        /// <summary>
        /// Gets the action function that loads text from a file.
        /// </summary>
        public override Func<FileInfo, string[]> Act
        {
            get
            {
                return (file) =>
                {
                    if (!file.Exists)
                    {
                        throw new FileNotFoundException($"File not found: {file.FullName}");
                    }

                    return File.ReadAllLines(file.FullName);
                };
            }
        }

        /// <summary>
        /// Validates that the word represents a valid file path or reference.
        /// </summary>
        public override bool Validate(IWord word)
        {
            // Accept any literal, variable, or reference word
            // File existence will be checked during execution
            return word is LiteralWord or VariableWord or ReferenceWord;
        }

        /// <summary>
        /// Resolves a string value to FileInfo.
        /// </summary>
        public override FileInfo? Resolve(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
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
            return Resolve(reference.Reference);
        }

        /// <summary>
        /// Resolves a VariableWord to FileInfo by looking up the variable value.
        /// </summary>
        public FileInfo? Resolve(VariableWord variable)
        {
            // Variable resolution will be handled by the engine
            // This overload allows the engine to detect we support variables
            return null;
        }

        /// <summary>
        /// Resolves the WHAT parameter for LOAD verbs.
        /// For LOAD, WHAT is an output target (where to store loaded data), not input data.
        /// Returns empty array as placeholder - actual result comes from Act execution.
        /// </summary>
        public string[] ResolveWhat(string value)
        {
            // LOAD's WHAT is output, not input - return empty array as placeholder
            return Array.Empty<string>();
        }
    }
}