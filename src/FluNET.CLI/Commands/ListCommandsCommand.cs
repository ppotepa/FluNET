using System;

namespace FluNET.CLI.Commands;

public class ListCommandsCommand : ICommand
{
    public string Name => "LIST COMMANDS";
    public string[] Aliases => new[] { "COMMANDS", "LIST" };
    public string Description => "Show all available meta-commands";
    public string Usage => "LIST COMMANDS";

    public bool Execute(string args, CliContext context)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Available Commands:");
        Console.ResetColor();
        Console.WriteLine();

        var commands = CommandFactory.DiscoverCommands();

        foreach (var command in commands.OrderBy(c => c.Name))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  {command.Name}");
            Console.ResetColor();

            if (command.Aliases.Any())
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" (Aliases: {string.Join(", ", command.Aliases)})");
                Console.ResetColor();
            }

            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("    Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(command.Usage);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"    {command.Description}");
            Console.ResetColor();

            Console.WriteLine();
        }

        return true; // Continue CLI
    }
}
