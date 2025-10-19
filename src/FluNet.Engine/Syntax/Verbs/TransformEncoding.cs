namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of TRANSFORM verb for encoding text using a specific encoding.
    /// Usage: TRANSFORM [text] USING [UTF8]
    /// </summary>
    public class TransformEncoding : Transform<string, System.Text.Encoding>
    {
        /// <summary>
        /// Initializes a new instance of TransformEncoding.
        /// </summary>
        /// <param name="what">The text to encode</param>
        /// <param name="using">The encoding to use</param>
        public TransformEncoding(string what, System.Text.Encoding @using) : base(what, @using)
        {
        }

        /// <summary>
        /// Gets the action function that encodes text using the specified encoding.
        /// </summary>
        public override Func<System.Text.Encoding, string> Act
        {
            get
            {
                return (encoding) =>
                {
                    byte[] bytes = encoding.GetBytes(What);
                    return Convert.ToBase64String(bytes);
                };
            }
        }
    }
}
