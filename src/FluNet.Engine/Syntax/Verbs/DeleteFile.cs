using FluNET.Words;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of DELETE verb for removing a file.
    /// Usage: DELETE [file] FROM [directory]
    /// </summary>
    public class DeleteFile : Delete<string, DirectoryInfo>
    {
        /// <summary>
        /// Initializes a new instance of DeleteFile.
        /// </summary>
        /// <param name="what">The filename to delete</param>
        /// <param name="from">The directory containing the file</param>
        public DeleteFile(string what, DirectoryInfo from) : base(what, from)
        {
        }

        /// <summary>
        /// Gets the action function that deletes a file from a directory.
        /// </summary>
        public override Func<DirectoryInfo, string> Act
        {
            get
            {
                return (directory) =>
                {
                    string fullPath = Path.Combine(directory.FullName, What);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        return $"Deleted: {What}";
                    }
                    return $"File not found: {What}";
                };
            }
        }

        /// <summary>
        /// Validates that the word represents a valid directory path.
        /// </summary>
        public override bool Validate(IWord word)
        {
            return word is LiteralWord or VariableWord or ReferenceWord;
        }

        /// <summary>
        /// Resolves a string value to DirectoryInfo.
        /// </summary>
        public override DirectoryInfo? Resolve(string value)
        {
            try
            {
                return new DirectoryInfo(value);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a ReferenceWord to DirectoryInfo.
        /// </summary>
        public DirectoryInfo? Resolve(ReferenceWord reference)
        {
            try
            {
                return new DirectoryInfo(reference.Reference);
            }
            catch
            {
                return null;
            }
        }
    }
}