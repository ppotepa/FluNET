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
                    var config = new Dictionary<string, object>();
                    // Simplified example - would need proper JSON parsing
                    config["loaded"] = true;
                    config["source"] = file.Name;
                    return config;
                };
            }
        }
    }
}
