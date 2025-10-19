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
    }
}
