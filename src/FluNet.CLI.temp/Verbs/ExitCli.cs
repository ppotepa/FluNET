namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// CLI verb for exiting the application.
    /// Usage: EXIT (or QUIT as alias)
    /// </summary>
    public class ExitCli : CliVerb<string>
    {
        public ExitCli() : this(string.Empty)
        {
        }

        public ExitCli(string what) : base(what)
        {
        }

        public override string Text => "EXIT";

        public override string[] Synonyms => new[] { "QUIT" };

        protected override bool IsValidSubject(string subject)
        {
            // EXIT doesn't need a subject, it's self-contained
            return true;
        }

        public override void Execute()
        {
            // This method is called by the base class but we return the signal via ExecuteWithResult
        }

        public bool ExecuteWithResult()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Goodbye!");
            Console.ResetColor();
            return true; // Signal to exit the loop
        }
    }
}