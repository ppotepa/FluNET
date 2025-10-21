using System;

namespace FluNET.CLI.Commands;

public class ExitCommand : ICommand
{
    public string Name => "EXIT";
    public string[] Aliases => new[] { "QUIT" };
    public string Description => "Exit the application";
    public string Usage => "EXIT";

    public bool Execute(string args, CliContext context)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Goodbye!");
        Console.ResetColor();
        return false; // Exit CLI
    }
}
