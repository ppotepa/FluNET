namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// CLI verb for displaying help information.
    /// Usage: HELP (or ? as alias)
    /// Can also be used as: SHOW HELP
    /// </summary>
    public class ShowHelp : CliVerb<string>
    {
        public ShowHelp() : this(string.Empty)
        {
        }

        public ShowHelp(string what) : base(what)
        {
        }

        public override string Text => "HELP";

        public override string[] Synonyms => new[] { "?" };

        protected override bool IsValidSubject(string subject)
        {
            // HELP doesn't need a specific subject, it's self-contained
            return true;
        }

        public override void Execute()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Available Meta-Commands:");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  HELP, ?           - Show this help message");
            Console.WriteLine("  LIST VERBS        - Show all available verbs and their usage");
            Console.WriteLine("  SHOW HISTORY      - Show command history");
            Console.WriteLine("  SHOW VARIABLES    - Show registered variables");
            Console.WriteLine("  SET [var] TO value- Register a variable");
            Console.WriteLine("  CLEAR SCREEN      - Clear the console");
            Console.WriteLine("  EXIT              - Exit the application");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("FluNET Sentence Syntax:");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  VERB [what] FROM [source] TO [destination].");
            Console.WriteLine();
            Console.WriteLine("  Important: Sentences must end with a period (.)");
            Console.WriteLine("             CLI aliases can omit the period");
            Console.WriteLine();
            Console.WriteLine("  Examples:");
            Console.WriteLine("    GET [text] FROM file.txt.");
            Console.WriteLine("    DOWNLOAD https://example.com/file.pdf TO ./downloads/.");
            Console.WriteLine("    SAVE [myVar] TO output.txt.");
            Console.WriteLine();
            Console.WriteLine("  Aliases (period optional):");
            Console.WriteLine("    CLEAR  or  CLS");
            Console.WriteLine("    VARIABLES  or  VARS");
            Console.WriteLine("    HISTORY");
            Console.WriteLine("    HELP  or  ?");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}