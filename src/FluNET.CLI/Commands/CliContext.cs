namespace FluNET.CLI.Commands;

/// <summary>
/// Context passed to commands for access to CLI state
/// </summary>
public class CliContext
{
    public Engine Engine { get; }
    public List<string> CommandHistory { get; }

    public CliContext(Engine engine, List<string> commandHistory)
    {
        Engine = engine;
        CommandHistory = commandHistory;
    }
}
