using FluNET.Syntax.Core;
using FluNET.Words;
using FluNET.Capabilities;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of the SAY verb for outputting text to the console.
    /// Usage: SAY [message]
    /// Examples:
    ///   - SAY Hello World.
    ///   - SAY [variableName].
    ///   - SAY {filepath}.
    /// Synonyms: ECHO, PRINT, OUTPUT, WRITE
    /// </summary>
    public class SayText : Say<string>, IAsyncVerb
    {
        private readonly ITextOutput _output;
        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public SayText() : this(string.Empty, DefaultCapabilities.Output)
        {
        }

        /// <summary>
        /// Initializes a new instance of SayText.
        /// </summary>
        /// <param name="what">The text message to output</param>
        public SayText(string what) : this(what, DefaultCapabilities.Output)
        {
        }

        public SayText(string what, ITextOutput output) : base(what)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// Gets the action function that outputs the text to the console.
        /// </summary>
        public override Func<string, string> Act
        {
            get
            {
                return (message) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[SayText.Act] Received message: '{message}'");

                    // If message looks like a type name (e.g., "System.String[]"), it means we didn't
                    // properly resolve the variable - this shouldn't happen with proper variable resolution
                    if (message.StartsWith("System.") && message.Contains("[]"))
                    {
                        System.Diagnostics.Debug.WriteLine($"[SayText.Act] WARNING: Received type name instead of value: {message}");
                    }

                    return InvokeAsync().AsTask().GetAwaiter().GetResult() as string ?? message;
                };
            }
        }

        public async ValueTask<object?> InvokeAsync(CancellationToken cancellationToken = default)
        {
            await _output.WriteLineAsync(What, cancellationToken).ConfigureAwait(false);
            return What;
        }

        /// <summary>
        /// Validates that the word represents valid output text.
        /// Accepts literals, variables, and references.
        /// </summary>
        public override bool Validate(IWord word)
        {
            // For literal words, accept any text
            if (word is LiteralWord literalWord)
            {
                // Accept any literal (including empty, for flexibility)
                return true;
            }

            // For variable words, we'll resolve them at execution time
            if (word is VariableWord)
            {
                return true;
            }

            // For reference words {value}, accept them
            if (word is ReferenceWord)
            {
                return true;
            }

            return word is QualifierWord;
        }

        /// <summary>
        /// Resolves a string value to string (pass-through for SAY).
        /// This allows SAY [message] to work contextually.
        /// </summary>
        /// <param name="value">The message string</param>
        /// <returns>The message string</returns>
        public string? Resolve(string value)
        {
            // For SAY, just return the string as-is
            return value;
        }

        /// <summary>
        /// Resolves a ReferenceWord to string.
        /// </summary>
        public string? Resolve(ReferenceWord reference)
        {
            return reference.ResolveAs<string>();
        }
    }
}
