using FluNET.Syntax.Core;
using FluNET.Words;

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
    public class SayText : Say<string>
    {
        /// <summary>
        /// Parameterless constructor for WordFactory discovery.
        /// </summary>
        public SayText() : base(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of SayText.
        /// </summary>
        /// <param name="what">The text message to output</param>
        public SayText(string what) : base(what)
        {
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

                    // Output the message to the console
                    Console.WriteLine(message);
                    System.Diagnostics.Debug.WriteLine($"[SayText.Act] After Console.WriteLine");

                    // Return the message so it can be used in THEN chains
                    return message;
                };
            }
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

            return false;
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