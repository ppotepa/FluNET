using FluNET.Syntax.Core;
using FluNET.Words;
using FluNET.Capabilities;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of SAVE verb for writing text to a file.
    /// Usage: SAVE [text] TO [output.txt]
    /// </summary>
    public class SaveText : Save<string, FileInfo>, IAsyncVerb
    {
        private readonly IFluNetFileSystem _fileSystem;
        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public SaveText() : this(string.Empty, new FileInfo("temp"), DefaultCapabilities.FileSystem)
        {
        }

        /// <summary>
        /// Initializes a new instance of SaveText.
        /// </summary>
        /// <param name="what">The text to save</param>
        /// <param name="to">The file to save to</param>
        public SaveText(string what, FileInfo to) : this(what, to, DefaultCapabilities.FileSystem)
        {
        }

        public SaveText(string what, FileInfo to, IFluNetFileSystem fileSystem) : base(what, to)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Gets the action function that writes text to a file.
        /// </summary>
        public override Func<FileInfo, string> Act
        {
            get
            {
                return (file) =>
                {
                    _fileSystem.WriteAllTextAsync(file.FullName, What).GetAwaiter().GetResult();
                    return What;
                };
            }
        }

        public async ValueTask<object?> InvokeAsync(CancellationToken cancellationToken = default)
        {
            await _fileSystem.WriteAllTextAsync(To.FullName, What, cancellationToken).ConfigureAwait(false);
            return What;
        }

        /// <summary>
        /// Validates that the word represents a valid file path for saving.
        /// </summary>
        public override bool Validate(IWord word)
        {
            // For saving, we just need a valid path (file doesn't need to exist)
            return word is LiteralWord or VariableWord or ReferenceWord;
        }

        /// <summary>
        /// Resolves a string value to FileInfo for file saving.
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
        /// Resolves a ReferenceWord to FileInfo for the TO parameter.
        /// </summary>
        public FileInfo? Resolve(ReferenceWord reference)
        {
            return reference.ResolveAs<FileInfo>();
        }
    }
}
