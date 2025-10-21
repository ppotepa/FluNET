namespace FluNET.CLI.Commands;

public class VariablesCommand : ICommand
{
    public string Name => "VARIABLES";
    public string[] Aliases => new[] { "VARS" };
    public string Description => "Show registered variables";
    public string Usage => "VARIABLES";

    public bool Execute(string args, CliContext context)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Registered Variables:");
        Console.ResetColor();
        Console.WriteLine();

        // Use reflection to access the variable store
        var engineType = context.Engine.GetType();
        var variablesField = engineType.GetField("_variables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var variables = variablesField?.GetValue(context.Engine) as Dictionary<string, object>;

        if (variables == null || !variables.Any())
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("  No variables registered.");
            Console.ResetColor();
            Console.WriteLine();
            return true;
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
        return true; // Continue CLI
    }
}
