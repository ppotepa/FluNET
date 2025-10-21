using System;

namespace FluNET.CLI.Commands;

public class HelpCommand : ICommand
{
    public string Name => "HELP";
    public string[] Aliases => new[] { "?" };
    public string Description => "Show this help message";
    public string Usage => "HELP";

    public bool Execute(string args, CliContext context)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Available Meta-Commands:");
        Console.ResetColor();
        Console.WriteLine();

        // Discover all commands
        var commands = CommandFactory.DiscoverCommands();

        foreach (var command in commands.OrderBy(c => c.Name))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"  {command.Name}");

            if (command.Aliases.Any())
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($", {string.Join(", ", command.Aliases)}");
            }

            Console.ResetColor();
            Console.Write(" - ");
            Console.WriteLine(command.Description);

            if (!string.IsNullOrEmpty(command.Usage) && command.Usage != command.Name)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"      Usage: {command.Usage}");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("FluNET Sentence Syntax:");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  VERB [what] FROM [source] TO [destination].");
        Console.WriteLine();
        Console.WriteLine("  Important: Sentences must end with a period (.)");
        Console.WriteLine();
        Console.WriteLine("  Examples:");
        Console.WriteLine("    GET [text] FROM file.txt.");
        Console.WriteLine("    DOWNLOAD https://example.com/file.pdf TO ./downloads/.");
        Console.WriteLine("    SAVE [myVar] TO output.txt.");
        Console.ResetColor();
        Console.WriteLine();

        return true; // Continue CLI
    }
}
