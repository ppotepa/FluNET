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
    }
}
