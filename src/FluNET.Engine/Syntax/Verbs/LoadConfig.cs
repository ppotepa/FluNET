using FluNET.Syntax.Core;
using FluNET.Words;
using FluNET.Capabilities;
using System.Text.Json;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of LOAD verb for loading configuration from a JSON file.
    /// Usage: LOAD [config] FROM [settings.json]
    /// </summary>
    public class LoadConfig : Load<Dictionary<string, object>, FileInfo>, IAsyncVerb
    {
        private readonly IFluNetFileSystem _fileSystem;
        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public LoadConfig() : this(
            new Dictionary<string, object>(),
            new FileInfo("temp"),
            DefaultCapabilities.FileSystem)
        {
        }

        /// <summary>
        /// Initializes a new instance of LoadConfig.
        /// </summary>
        /// <param name="what">The configuration dictionary to load into</param>
        /// <param name="from">The file to load from</param>
        public LoadConfig(Dictionary<string, object> what, FileInfo from) : this(
            what,
            from,
            DefaultCapabilities.FileSystem)
        {
        }

        public LoadConfig(
            Dictionary<string, object> what,
            FileInfo from,
            IFluNetFileSystem fileSystem) : base(what, from)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
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
                    return LoadAsync(file).GetAwaiter().GetResult();
                };
            }
        }

        public async ValueTask<object?> InvokeAsync(CancellationToken cancellationToken = default) =>
            await LoadAsync(From, cancellationToken).ConfigureAwait(false);

        private async Task<Dictionary<string, object>> LoadAsync(
            FileInfo file,
            CancellationToken cancellationToken = default)
        {
            string json = await _fileSystem.ReadAllTextAsync(file.FullName, cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                ?? throw new InvalidDataException($"Configuration file '{file.FullName}' contains JSON null.");
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

        /// <summary>
        /// Resolves the WHAT parameter (config name) to an empty Dictionary.
        /// The actual config data will be loaded by Act from the file.
        /// </summary>
        public Dictionary<string, object> ResolveWhat(string value)
        {
            // Create empty dictionary - Act will populate it from file
            return new Dictionary<string, object>();
        }
    }
}
