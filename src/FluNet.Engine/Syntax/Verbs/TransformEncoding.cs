using FluNET.Words;

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

        /// <summary>
        /// Validates that the word represents a valid encoding specification.
        /// </summary>
        public override bool Validate(IWord word)
        {
            // For encoding transformation, accept literal or variable words
            return word is LiteralWord or VariableWord or ReferenceWord;
        }

        /// <summary>
        /// Resolves a string value to System.Text.Encoding.
        /// </summary>
        public override System.Text.Encoding? Resolve(string value)
        {
            try
            {
                // Common encoding names: UTF8, UTF32, ASCII, Unicode
                return value.ToUpper() switch
                {
                    "UTF8" or "UTF-8" => System.Text.Encoding.UTF8,
                    "UTF32" or "UTF-32" => System.Text.Encoding.UTF32,
                    "ASCII" => System.Text.Encoding.ASCII,
                    "UNICODE" => System.Text.Encoding.Unicode,
                    _ => System.Text.Encoding.GetEncoding(value)
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a ReferenceWord to System.Text.Encoding.
        /// </summary>
        public System.Text.Encoding? Resolve(ReferenceWord reference)
        {
            return Resolve(reference.Reference);
        }
    }
}