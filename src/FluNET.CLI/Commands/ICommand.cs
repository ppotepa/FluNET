namespace FluNET.CLI.Commands;

/// <summary>
/// Represents a CLI meta-command (like HELP, EXIT, LIST VERBS)
/// </summary>
public interface ICommand
{
    /// <summary>
    /// The primary command name (e.g., "HELP", "EXIT")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Alternative names for this command (e.g., ["?"] for HELP, ["QUIT"] for EXIT)
    /// </summary>
    string[] Aliases { get; }

    /// <summary>
    /// Brief description of what the command does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Usage syntax (e.g., "SET <name> <value>")
    /// </summary>
    string Usage { get; }

    /// <summary>
    /// Execute the command
    /// </summary>
    /// <param name="args">Command arguments (everything after the command name)</param>
    /// <param name="context">CLI context with access to engine, history, etc.</param>
    /// <returns>True if CLI should continue running, false to exit</returns>
    bool Execute(string args, CliContext context);
}
