namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// CLI verb for showing command history.
    /// Usage: SHOW HISTORY (or just HISTORY as alias)
    /// Proper syntax: SHOW [what] where [what] = HISTORY
    /// </summary>
    public class ShowHistory : CliVerb<string>
    {
        private List<string>? _commandHistory;

        public ShowHistory() : this(string.Empty)
        {
        }

        public ShowHistory(string what) : base(what)
        {
        }

        public override string Text => "SHOW";

        public override string[] Synonyms => new[] { "HISTORY" };

        protected override bool IsValidSubject(string subject)
        {
            // HISTORY is the valid subject for SHOW in this context
            return subject == "HISTORY";
        }

        public void SetCommandHistory(List<string> commandHistory)
        {
            _commandHistory = commandHistory;
        }

        public override void Execute()
        {
            Execute(_commandHistory ?? new List<string>());
        }

        public void Execute(List<string> commandHistory)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Command History:");
            Console.ResetColor();
            Console.WriteLine();

            if (!commandHistory.Any())
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("  No commands in history.");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            for (int i = 0; i < commandHistory.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"  {i + 1,3}. ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(commandHistory[i]);
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }
}
