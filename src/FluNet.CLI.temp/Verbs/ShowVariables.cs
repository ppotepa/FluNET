namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// CLI verb for showing registered variables.
    /// Usage: SHOW VARIABLES (or SHOW VARS, or just VARIABLES/VARS as alias)
    /// When used as alias, VARIABLES/VARS acts as both verb and subject
    /// </summary>
    public class ShowVariables : CliVerb<string>
    {
        private Engine? _engine;

        public ShowVariables() : this(string.Empty)
        {
        }

        public ShowVariables(string what) : base(what)
        {
        }

        public override string Text => "VARIABLES";

        public override string[] Synonyms => new[] { "VARS" };

        protected override bool IsValidSubject(string subject)
        {
            // When used after SHOW, accept empty (for alias usage)
            // SHOW doesn't need a subject when VARIABLES is the verb itself
            return true;
        }

        public void SetEngine(Engine engine)
        {
            _engine = engine;
        }

        public override void Execute()
        {
            if (_engine != null)
            {
                Execute(_engine);
            }
        }

        public void Execute(Engine engine)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Registered Variables:");
            Console.ResetColor();
            Console.WriteLine();

            // Use reflection to access the variable store
            var engineType = engine.GetType();
            var variablesField = engineType.GetField("_variables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var variables = variablesField?.GetValue(engine) as Dictionary<string, object>;

            if (variables == null || !variables.Any())
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("  No variables registered.");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            foreach (var kvp in variables.OrderBy(v => v.Key))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"  [{kvp.Key}]");
                Console.ResetColor();
                Console.Write(" = ");

                Console.ForegroundColor = ConsoleColor.White;
                var valueDisplay = kvp.Value?.ToString() ?? "null";
                if (valueDisplay.Length > 50)
                    valueDisplay = valueDisplay.Substring(0, 47) + "...";

                Console.WriteLine(valueDisplay);
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"      Type: {kvp.Value?.GetType().Name ?? "null"}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }
}