using FluNET.CLI.Verbs;
using FluNET.Sentences;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;
using FluNET.Context;

namespace FluNET.CLI;

public static class Program
{
    private static FluNetContext? _context;
    private static Engine? _engine;
    private static DiscoveryService? _discoveryService;
    private static readonly List<string> _commandHistory = new();
    private static readonly string[] CliAliases = { "CLEAR", "CLS", "VARIABLES", "VARS", "HISTORY", "HELP", "?", "EXIT", "QUIT" };

    private static void Main(string[] args)
    {
        // Force CLI assembly types to be loaded before DiscoveryService scans
        // This ensures CLI verbs are discovered along with application verbs
        EnsureCliTypesLoaded();

        // Setup using FluNetContext with customization for CLI
        _context = FluNetContext.Create(services =>
        {
            // CLI uses PersistentVariableResolver so variables persist across commands
            // Replace the default scoped IVariableResolver with singleton
            services.AddSingleton<IVariableResolver, PersistentVariableResolver>();
        });

        _engine = _context.GetEngine();
        _discoveryService = _context.GetService<DiscoveryService>();

        // Display welcome banner
        DisplayWelcomeBanner();

        // Interactive prompt loop
        while (true)
        {
            // Display prompt
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("FluNET> ");
            Console.ResetColor();

            // Read user input
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Add to history (before trimming, to preserve what user typed)
            _commandHistory.Add(input);

            // Trim input
            string trimmedInput = input.Trim();

            // Process sentence - CLI aliases can omit the period
            if (!ProcessSentence(trimmedInput))
            {
                break; // EXIT command executed
            }
        }
    }

    public static void DisplayWelcomeBanner()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                   FluNET Interactive CLI                   ║");
        Console.WriteLine("║              Natural Language Command Processor            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("Type 'HELP' to see available CLI commands.");
        Console.WriteLine("Type 'LIST VERBS' to see all available FluNET operations.");
        Console.WriteLine("Type 'EXIT' to quit.");
        Console.WriteLine();
        Console.WriteLine("CLI aliases (CLEAR, HELP, EXIT, VARIABLES, HISTORY) can omit the period.");
        Console.WriteLine();
        Console.ResetColor();
    }

    /// <summary>
    /// Process a sentence through the FluNET engine.
    /// CLI aliases (CLEAR, HELP, EXIT, etc.) can omit the period terminator.
    /// Returns false if EXIT was requested.
    /// </summary>
    private static bool ProcessSentence(string sentence)
    {
        if (_engine == null)
            return true;

        try
        {
            // Check if this is a CLI alias (can omit period)
            bool isCliAlias = IsCliAlias(sentence);

            // Add period if missing (required for proper tokenization)
            // CLI aliases are exempt from the warning
            if (!sentence.EndsWith("."))
            {
                if (!isCliAlias)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠ Adding missing terminator (.). Sentences must end with a period.");
                    Console.ResetColor();
                }
                sentence += ".";
            }

            var processedPrompt = new Prompt.ProcessedPrompt(sentence);

            // Don't show "Executing..." for CLI commands
            if (!isCliAlias)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Executing...");
                Console.ResetColor();
            }

            (ValidationResult validation, ISentence? sentenceObj, object? result) = _engine.Run(processedPrompt);

            if (!validation.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Validation failed: {validation.FailureReason}");
                Console.ResetColor();
                Console.WriteLine();
                return true;
            }

            // Check if this is a CLI verb and handle accordingly
            if (sentenceObj?.Root is IVerb verb)
            {
                // Handle CLI-specific verbs
                if (verb is ExitCli exitVerb)
                {
                    exitVerb.Execute();
                    return false; // Signal to exit
                }
                else if (verb is ClearScreen clearVerb)
                {
                    clearVerb.Execute();
                    return true;
                }
                else if (verb is ShowHelp helpVerb)
                {
                    helpVerb.Execute();
                    return true;
                }
                else if (verb is ShowHistory historyVerb)
                {
                    historyVerb.Execute(_commandHistory);
                    return true;
                }
                else if (verb is ShowVariables varsVerb)
                {
                    varsVerb.Execute(_engine);
                    return true;
                }
                else if (verb is ListVerbs listVerb)
                {
                    if (_discoveryService != null)
                    {
                        listVerb.Execute(_discoveryService);
                    }
                    return true;
                }
                else if (verb is SetVariable setVerb)
                {
                    // Extract variable name and value from sentence
                    var varWord = sentenceObj.Root.Next as VariableWord;
                    if (varWord != null)
                    {
                        // Find TO keyword and get value after it
                        IWord? current = varWord.Next;
                        while (current != null)
                        {
                            if (current is Keywords.To toKeyword && current.Next is LiteralWord valueLiteral)
                            {
                                string varName = varWord.VariableReference.Trim('[', ']');
                                string value = valueLiteral.Value.TrimEnd('.');
                                setVerb.Execute(_engine, varName, value);
                                return true;
                            }
                            current = current.Next;
                        }
                    }
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Invalid SET syntax. Usage: SET [variableName] TO value.");
                    Console.ResetColor();
                    Console.WriteLine();
                    return true;
                }
            }

            // Regular FluNET verb execution - show results
            if (!isCliAlias)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Execution completed successfully.");
                Console.ResetColor();

                if (result != null)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Result:");
                    Console.ResetColor();

                    if (result is string[] lines)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"  ({lines.Length} lines)");
                        foreach (string line in lines)
                        {
                            Console.WriteLine($"  {line}");
                        }
                        Console.ResetColor();
                    }
                    else if (result is byte[] bytes)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"  Binary data: {bytes.Length} bytes");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        var resultStr = result.ToString();
                        if (resultStr != null && resultStr.Length > 200)
                        {
                            Console.WriteLine($"  {resultStr.Substring(0, 197)}...");
                        }
                        else
                        {
                            Console.WriteLine($"  {resultStr}");
                        }
                        Console.ResetColor();
                    }
                }

                Console.WriteLine();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();

            if (ex.InnerException != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
                Console.ResetColor();
            }

            Console.WriteLine();
            return true;
        }
    }

    /// <summary>
    /// Check if the sentence is a CLI alias (commands that can omit the period).
    /// </summary>
    private static bool IsCliAlias(string sentence)
    {
        string upperSentence = sentence.TrimEnd('.').ToUpperInvariant();
        return CliAliases.Any(alias => upperSentence == alias || upperSentence.StartsWith(alias + " "));
    }

    /// <summary>
    /// Ensures CLI verb types are loaded into the AppDomain before DiscoveryService scans.
    /// This is necessary because DiscoveryService only discovers types from loaded assemblies.
    /// </summary>
    private static void EnsureCliTypesLoaded()
    {
        // Reference CLI verb types to force assembly loading
        Type[] cliVerbs = new[]
        {
            typeof(ClearScreen),
            typeof(ListVerbs),
            typeof(ShowHelp),
            typeof(ShowHistory),
            typeof(ShowVariables),
            typeof(ExitCli),
            typeof(SetVariable)
        };

        // This forces the JIT to load the types and their containing assembly
        foreach (var verbType in cliVerbs)
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(verbType.TypeHandle);
        }
    }
}