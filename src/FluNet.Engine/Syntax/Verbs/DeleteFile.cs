using FluNET.Syntax.Core;
using FluNET.Words;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of DELETE verb for removing a file.
    /// Usage:
    ///   DELETE [filepath] - Full path to file
    ///   DELETE [filename] FROM [directory] - Filename and directory separately
    /// Examples:
    ///   DELETE test.txt
    ///   DELETE C:\temp\test.txt
    ///   DELETE test.txt FROM C:\temp
    /// </summary>
    public class DeleteFile : Delete<string, DirectoryInfo>
    {
        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public DeleteFile() : base(string.Empty, new DirectoryInfo("."))
        {
        }

        /// <summary>
        /// Initializes a new instance of DeleteFile.
        /// </summary>
        /// <param name="what">The filename to delete</param>
        /// <param name="from">The directory containing the file (optional - defaults to current directory if null)</param>
        public DeleteFile(string what, DirectoryInfo? from) : base(what, from)
        {
        }

        /// <summary>
        /// Gets the action function that deletes a file from a directory.
        /// Handles both full paths and filename+directory combinations.
        /// If directory is null, uses current directory as default.
        /// </summary>
        public override Func<DirectoryInfo?, string> Act
        {
            get
            {
                return (directory) =>
                {
                    // Use current directory if none provided
                    DirectoryInfo actualDirectory = directory ?? new DirectoryInfo(".");

                    string fullPath;

                    // Check if What contains a full path (has directory separators)
                    if (What.Contains(Path.DirectorySeparatorChar) || What.Contains(Path.AltDirectorySeparatorChar))
                    {
                        // What is a full path - use it directly
                        fullPath = What;
                    }
                    else
                    {
                        // What is just a filename - combine with directory
                        fullPath = Path.Combine(actualDirectory.FullName, What);
                    }

                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        return $"Deleted: {fullPath}";
                    }
                    return $"File not found: {fullPath}";
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