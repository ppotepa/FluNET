namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of SAVE verb for writing text to a file.
    /// Usage: SAVE [text] TO [output.txt]
    /// </summary>
    public class SaveText : Save<string, FileInfo>
    {
        /// <summary>
        /// Initializes a new instance of SaveText.
        /// </summary>
        /// <param name="what">The text to save</param>
        /// <param name="to">The file to save to</param>
        public SaveText(string what, FileInfo to) : base(what, to)
        {
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
                    File.WriteAllText(file.FullName, What);
                    return What;
                };
            }
        }
    }
}
