using FluNET.Syntax.Core;
using FluNET.Words;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of LOAD verb for loading configuration from a JSON file.
    /// Usage: LOAD [config] FROM [settings.json]
    /// </summary>
    public class LoadConfig : Load<Dictionary<string, object>, FileInfo>
    {
        /// <summary>
        /// Initializes a new instance of LoadConfig.
        /// </summary>
        /// <param name="what">The configuration dictionary to load into</param>
        /// <param name="from">The file to load from</param>
        public LoadConfig(Dictionary<string, object> what, FileInfo from) : base(what, from)
        {
        }

        /// <summary>
        /// Gets the action function that loads configuration from a JSON file.
        /// </summary>
        public override Func<FileInfo, Dictionary<string, object>> Act
        {
            get
            {
                return (file) =>
                {
                    string json = File.ReadAllText(file.FullName);
                    // Simple JSON parsing - in production use System.Text.Json
                    Dictionary<string, object> config = new()
                    {
                        // Simplified example - would need proper JSON parsing
                        ["loaded"] = true,
                        ["source"] = file.Name
                    };
                    return config;
                };
            }
        }

        /// <summary>
        /// Validates that the word represents a valid config file path.
        /// </summary>
        public override bool Validate(IWord word)
        {
            if (word is LiteralWord litWord)
            {
                // Accept any non-empty literal as a potential file path
                // File existence will be checked during execution
                return !string.IsNullOrWhiteSpace(litWord.Value.TrimEnd('.'));
            }
            return word is VariableWord or ReferenceWord;
        }

        /// <summary>
        /// Resolves a string value to FileInfo for config files.
        /// </summary>
        public override FileInfo? Resolve(string value)
        {
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
            return reference.ResolveAs<FileInfo>();
        }
    }
}