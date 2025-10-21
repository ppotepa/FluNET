using FluNET.Words;
using System.Text;

namespace FluNET.CLI.Commands;

public class ListVerbsCommand : ICommand
{
    public string Name => "LIST VERBS";
    public string[] Aliases => new[] { "VERBS" };
    public string Description => "Show all available verbs and their usage";
    public string Usage => "LIST VERBS";

    public bool Execute(string args, CliContext context)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Available Verbs:");
        Console.ResetColor();
        Console.WriteLine();

        var wordFactory = GetWordFactory(context.Engine);
        if (wordFactory == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Could not access WordFactory.");
            Console.ResetColor();
            Console.WriteLine();
            return true;
        }

        var verbs = DiscoverVerbs(wordFactory);

        if (!verbs.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("No verbs discovered.");
            Console.ResetColor();
            Console.WriteLine();
            return true;
        }

        foreach (var verbInfo in verbs.OrderBy(v => v.Name))
        {
            // Display verb name
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  {verbInfo.Name.ToUpperInvariant()}");
            Console.ResetColor();

            // Display synonyms if any
            if (verbInfo.Synonyms.Any())
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($" (Synonyms: {string.Join(", ", verbInfo.Synonyms.Select(s => s.ToUpperInvariant()))})");
                Console.ResetColor();
            }

            Console.WriteLine();

            // Display usage pattern
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("    Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(verbInfo.Usage);
            Console.ResetColor();

            // Display example if available
            if (!string.IsNullOrEmpty(verbInfo.Example))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"    Example: {verbInfo.Example}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        return true; // Continue CLI
    }

    private static WordFactory? GetWordFactory(Engine engine)
    {
        var engineType = engine.GetType();
        var wordFactoryField = engineType.GetField("_wordFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return wordFactoryField?.GetValue(engine) as WordFactory;
    }

    private static List<VerbInfo> DiscoverVerbs(WordFactory wordFactory)
    {
        var verbInfoList = new List<VerbInfo>();

        // Get all verb types from loaded assemblies
        var verbTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => IsVerbType(t))
            .ToList();

        foreach (var verbType in verbTypes)
        {
            try
            {
                var verbInstance = Activator.CreateInstance(verbType);
                if (verbInstance == null)
                    continue;

                var textProperty = verbType.GetProperty("Text");
                var synonymsProperty = verbType.GetProperty("Synonyms");

                string? verbText = textProperty?.GetValue(verbInstance) as string;
                if (string.IsNullOrEmpty(verbText))
                    continue;

                var synonyms = synonymsProperty?.GetValue(verbInstance) as string[] ?? Array.Empty<string>();

                // Determine usage pattern from verb type
                var usage = DetermineUsagePattern(verbType, verbText);

                // Try to get example from verb itself (if it has an Example property)
                var exampleProperty = verbType.GetProperty("Example");
                var example = exampleProperty?.GetValue(verbInstance) as string ?? "";

                verbInfoList.Add(new VerbInfo
                {
                    Name = verbText,
                    Synonyms = synonyms.ToList(),
                    Usage = usage,
                    Example = example
                });
            }
            catch
            {
                // Skip verbs that can't be instantiated
            }
        }

        return verbInfoList;
    }

    private static bool IsVerbType(Type type)
    {
        var interfaces = type.GetInterfaces();
        return interfaces.Any(i => i.Name.Contains("IVerb")) ||
               type.BaseType?.Name.Contains("Verb") == true;
    }

    private static string DetermineUsagePattern(Type verbType, string verbText)
    {
        var baseType = verbType.BaseType;
        if (baseType != null && baseType.IsGenericType)
        {
            var genericArgs = baseType.GetGenericArguments();
            if (genericArgs.Length >= 2)
            {
                var usage = new StringBuilder();
                usage.Append(verbText.ToUpperInvariant());
                usage.Append(" ");

                var whatType = genericArgs[0];
                var fromType = genericArgs.Length > 1 ? genericArgs[1] : null;
                var toType = genericArgs.Length > 2 ? genericArgs[2] : null;

                if (whatType.Name != "Empty")
                {
                    usage.Append("[what] ");
                }

                if (fromType != null && fromType.Name != "Empty")
                {
                    usage.Append("FROM [source] ");
                }

                if (toType != null && toType.Name != "Empty")
                {
                    usage.Append("TO [destination] ");
                }

                usage.Append(".");
                return usage.ToString().Trim();
            }
        }

        return $"{verbText.ToUpperInvariant()} [arguments].";
    }

    private class VerbInfo
    {
        public string Name { get; set; } = "";
        public List<string> Synonyms { get; set; } = new();
        public string Usage { get; set; } = "";
        public string Example { get; set; } = "";
    }
}
