using FluNET.Syntax.Core;
using FluNET.Words;

namespace FluNET.Syntax.Verbs
{
    /// <summary>
    /// Concrete implementation of SEND verb for sending email messages.
    /// Usage: SEND [message] TO [recipient@example.com]
    /// </summary>
    public class SendEmail : Send<string, string>
    {
        /// <summary>
        /// Initializes a new instance of SendEmail.
        /// </summary>
        /// <param name="what">The email message content</param>
        /// <param name="to">The recipient email address</param>
        public SendEmail(string what, string to) : base(what, to)
        {
        }

        /// <summary>
        /// Gets the action function that sends an email.
        /// </summary>
        public override Func<string, string> Act
        {
            get
            {
                return (recipient) =>
                {
                    // Simulated email sending - in production would use SMTP
                    System.Diagnostics.Debug.WriteLine($"Sending email to {recipient}");
                    System.Diagnostics.Debug.WriteLine($"Message: {What}");
                    return $"Email sent to {recipient}";
                };
            }
        }

        /// <summary>
        /// Validates that the word represents a valid email address.
        /// </summary>
        public override bool Validate(IWord word)
        {
            return word is LiteralWord or VariableWord or ReferenceWord;
        }

        /// <summary>
        /// Resolves a string value to string (email addresses are already strings).
        /// </summary>
        public override string? Resolve(string value)
        {
            // Email addresses are already strings, just return the value
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
