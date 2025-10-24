namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// CLI verb for clearing the console screen.
    /// Usage: CLEAR SCREEN (or just CLEAR, or CLS as alias)
    /// Proper syntax: CLEAR [what] where [what] = SCREEN
    /// </summary>
    public class ClearScreen : CliVerb<string>
    {
        public ClearScreen() : this(string.Empty)
        {
        }

        public ClearScreen(string what) : base(what)
        {
        }

        public override string Text => "CLEAR";

        public override string[] Synonyms => new[] { "CLS" };

        protected override bool IsValidSubject(string subject)
        {
            // SCREEN is the valid subject for CLEAR
            return subject == "SCREEN";
        }

        public override void Execute()
        {
            Console.Clear();
            DisplayWelcomeBanner();
        }

        private static void DisplayWelcomeBanner()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                   FluNET Interactive CLI                   ║");
            Console.WriteLine("║              Natural Language Command Processor            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Type 'HELP' for available commands or 'LIST VERBS' to see available operations.");
            Console.WriteLine("Type 'EXIT' to quit.");
            Console.WriteLine();
            Console.ResetColor();
        }
    }
}