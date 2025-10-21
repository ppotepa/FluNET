namespace FluNET.CLI.Commands;

public class SetCommand : ICommand
{
    public string Name => "SET";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Register a variable";
    public string Usage => "SET <name> <value>";

    public bool Execute(string args, CliContext context)
    {
        // Parse: variableName value
        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Invalid SET command. Usage: {Usage}");
            Console.ResetColor();
            Console.WriteLine();
            return true;
        }

        string varName = parts[0];
        string varValue = parts[1];

        try
        {
            context.Engine.RegisterVariable(varName, varValue);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Variable [{varName}] registered successfully.");
            Console.ResetColor();
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error registering variable: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
        }

        return true; // Continue CLI
    }
}
