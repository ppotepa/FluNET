using FluNET;
using FluNET.Syntax.Core;

var discovery = new DiscoveryService();

Console.WriteLine($"Total Words: {discovery.Words.Count}");
Console.WriteLine($"Total Verbs: {discovery.Verbs.Count}");
Console.WriteLine($"Total Nouns: {discovery.Nouns.Count}");
Console.WriteLine();

Console.WriteLine("Discovered Verbs:");
foreach (var verb in discovery.Verbs)
{
    Console.WriteLine($"  - {verb.Name} ({verb.Namespace})");

    try
    {
        var instance = Activator.CreateInstance(verb);
        if (instance is IVerb verbInstance)
        {
            var textProp = verb.GetProperty("Text");
            var text = textProp?.GetValue(instance);
            Console.WriteLine($"    Text: {text}");

            var synonymsProp = verb.GetProperty("Synonyms");
            var synonyms = synonymsProp?.GetValue(instance) as string[];
            if (synonyms != null && synonyms.Length > 0)
            {
                Console.WriteLine($"    Synonyms: {string.Join(", ", synonyms)}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    Error instantiating: {ex.Message}");
    }
    Console.WriteLine();
}
