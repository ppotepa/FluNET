namespace FluNET.CLI.Commands;

public class HistoryCommand : ICommand
{
    public string Name => "HISTORY";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Show command history";
    public string Usage => "HISTORY";

    public bool Execute(string args, CliContext context)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Command History:");
        Console.ResetColor();
        Console.WriteLine();

        if (!context.CommandHistory.Any())
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("  No commands in history.");
            Console.ResetColor();
            Console.WriteLine();
            return true;
        }

        for (int i = 0; i < context.CommandHistory.Count; i++)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"  {i + 1,3}. ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(context.CommandHistory[i]);
            Console.ResetColor();
        }

        Console.WriteLine();
        return true; // Continue CLI
    }
}
