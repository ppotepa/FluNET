using System;

namespace FluNET.CLI.Commands;

public class ClearCommand : ICommand
{
    public string Name => "CLEAR";
    public string[] Aliases => new[] { "CLS" };
    public string Description => "Clear the console";
    public string Usage => "CLEAR";

    public bool Execute(string args, CliContext context)
    {
        Console.Clear();
        Program.DisplayWelcomeBanner();
        return true; // Continue CLI
    }
}
