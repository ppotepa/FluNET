using FluNET.Words;

namespace FluNET.CLI.Verbs
{
    /// <summary>
    /// CLI verb for listing all available verbs.
    /// Usage: LIST VERBS
    /// Proper syntax: LIST [what] where [what] = VERBS
    /// </summary>
    public class ListVerbs : CliVerb<string>
    {
        private DiscoveryService? _discoveryService;

        public ListVerbs() : this(string.Empty)
        {
        }

        public ListVerbs(string what) : base(what)
        {
        }

        public override string Text => "LIST";

        public override string[] Synonyms => Array.Empty<string>();

        protected override bool IsValidSubject(string subject)
        {
            // VERBS is the valid subject for LIST
            // Also accept empty for alias usage (just "LIST")
            return subject == "VERBS" || string.IsNullOrEmpty(subject);
        }

        public void SetDiscoveryService(DiscoveryService discoveryService)
        {
            _discoveryService = discoveryService;
        }

        public override void Execute()
        {
            if (_discoveryService != null)
            {
                Execute(_discoveryService);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("DiscoveryService not available.");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        public void Execute(DiscoveryService discoveryService)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Available Verbs:");
            Console.ResetColor();
            Console.WriteLine();

            var verbs = DiscoverVerbs(discoveryService);

            if (!verbs.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No verbs discovered.");
                Console.ResetColor();
                Console.WriteLine();
                return;
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

                // Display example
                if (!string.IsNullOrEmpty(verbInfo.Example))
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"    Example: {verbInfo.Example}");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }

        private static List<VerbInfo> DiscoverVerbs(DiscoveryService discoveryService)
        {
            var verbInfoList = new List<VerbInfo>();

            // Get all verb types from DiscoveryService
            var verbTypes = discoveryService.Verbs
                .Where(t => !t.Namespace?.Contains("FluNET.CLI.Verbs") ?? true) // Exclude CLI verbs from listing
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

                    var usage = DetermineUsagePattern(verbType);
                    var example = GetVerbExample(verbText);

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

        private static string DetermineUsagePattern(Type verbType)
        {
            var baseType = verbType.BaseType;
            if (baseType != null && baseType.IsGenericType)
            {
                var genericArgs = baseType.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    var usage = new System.Text.StringBuilder();
                    var verbName = verbType.Name.Replace("Verb", "").Replace("`", "").ToUpperInvariant();
                    usage.Append(verbName);
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

            return "VERB [arguments].";
        }

        private static string GetVerbExample(string verbText)
        {
            return verbText.ToUpperInvariant() switch
            {
                "GET" => "GET [text] FROM file.txt.",
                "GETTEXT" => "GET [text] FROM file.txt.",
                "POST" => "POST [data] TO https://api.example.com/endpoint.",
                "SAVE" => "SAVE [data] TO output.txt.",
                "DOWNLOAD" => "DOWNLOAD https://example.com/file.pdf TO ./downloads/.",
                "PULL" => "PULL https://example.com/data.json TO ./data/.",
                "GRAB" => "GRAB https://example.com/image.png TO ./images/.",
                "OBTAIN" => "OBTAIN https://example.com/config.xml TO ./config/.",
                _ => ""
            };
        }

        private class VerbInfo
        {
            public string Name { get; set; } = "";
            public List<string> Synonyms { get; set; } = new();
            public string Usage { get; set; } = "";
            public string Example { get; set; } = "";
        }
    }
}