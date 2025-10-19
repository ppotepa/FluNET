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
                    Console.WriteLine($"Sending email to {recipient}");
                    Console.WriteLine($"Message: {What}");
                    return $"Email sent to {recipient}";
                };
            }
        }
    }
}
