using System.Reflection;

namespace FluNET.CLI.Commands;

/// <summary>
/// Factory for discovering and executing CLI commands
/// </summary>
public static class CommandFactory
{
    private static List<ICommand>? _cachedCommands;

    /// <summary>
    /// Discover all available commands
    /// </summary>
    public static List<ICommand> DiscoverCommands()
    {
        if (_cachedCommands != null)
            return _cachedCommands;

        _cachedCommands = new List<ICommand>();

        // Find all types implementing ICommand in this assembly
        var commandTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ICommand).IsAssignableFrom(t))
            .ToList();

        foreach (var commandType in commandTypes)
        {
            try
            {
                var command = Activator.CreateInstance(commandType) as ICommand;
                if (command != null)
                {
                    _cachedCommands.Add(command);
                }
            }
            catch
            {
                // Skip commands that can't be instantiated
            }
        }

        return _cachedCommands;
    }

    /// <summary>
    /// Find a command by name or alias
    /// </summary>
    public static ICommand? FindCommand(string input)
    {
        var commands = DiscoverCommands();
        var upperInput = input.ToUpperInvariant();

        // Try exact match on name first
        var command = commands.FirstOrDefault(c => c.Name.Equals(upperInput, StringComparison.OrdinalIgnoreCase));
        if (command != null)
            return command;

        // Try aliases
        command = commands.FirstOrDefault(c => 
            c.Aliases.Any(alias => alias.Equals(upperInput, StringComparison.OrdinalIgnoreCase)));
        if (command != null)
            return command;

        // Try partial match for multi-word commands (e.g., "LIST VERBS")
        command = commands.FirstOrDefault(c => 
            upperInput.StartsWith(c.Name, StringComparison.OrdinalIgnoreCase));

        return command;
    }

    /// <summary>
    /// Try to execute a command
    /// </summary>
    /// <returns>Tuple: (wasCommand, shouldContinue)</returns>
    public static (bool WasCommand, bool ShouldContinue) TryExecuteCommand(string input, CliContext context)
    {
        var command = FindCommand(input);
        if (command == null)
            return (false, true); // Not a command, continue CLI

        // Extract arguments (everything after the command name/alias)
        var upperInput = input.ToUpperInvariant();
        string args = "";

        // Find what was matched (name or alias)
        var matched = command.Name;
        if (upperInput.StartsWith(command.Name, StringComparison.OrdinalIgnoreCase))
        {
            matched = command.Name;
        }
        else
        {
            var matchedAlias = command.Aliases.FirstOrDefault(a => 
                upperInput.StartsWith(a, StringComparison.OrdinalIgnoreCase) ||
                upperInput.Equals(a, StringComparison.OrdinalIgnoreCase));
            if (matchedAlias != null)
            {
                matched = matchedAlias;
            }
        }

        // Extract args (handle exact match or prefix match)
        if (upperInput.Length > matched.Length)
        {
            args = input.Substring(matched.Length).Trim();
        }

        var shouldContinue = command.Execute(args, context);
        return (true, shouldContinue);
    }
}
