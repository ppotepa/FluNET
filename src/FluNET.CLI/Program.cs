using FluNET.Sentences;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.CLI;

internal static class Program
{
    private static void Main(string[] _)
    {
        Console.WriteLine("=== FluNET Sentence Execution Demo ===\n");

        // Setup DI container
        IServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<Engine>();
        serviceCollection.AddScoped<TokenTreeFactory>();
        serviceCollection.AddScoped<TokenFactory>();
        serviceCollection.AddScoped<Lexicon.Lexicon>();
        serviceCollection.AddScoped<WordFactory>();
        serviceCollection.AddScoped<SentenceValidator>();
        serviceCollection.AddScoped<SentenceFactory>();
        serviceCollection.AddScoped<VariableResolver>();
        serviceCollection.AddScoped<SentenceExecutor>();

        ServiceProvider provider = serviceCollection.BuildServiceProvider();
        Engine engine = provider.GetRequiredService<Engine>();

        // Example 1: Simple validation test
        Console.WriteLine("Example 1: Validating sentences");
        Console.WriteLine("--------------------------------");

        // Create test file
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "file.txt");
        File.WriteAllText(filePath, "this is a test file");
        Console.WriteLine($"Created test file: {filePath}");
        Console.WriteLine();

        // Note: Terminator must be attached to last token due to tokenizer design
        string prompt1 = $"GET [text] FROM {filePath}.";
        Console.WriteLine($"Prompt: GET [text] FROM {filePath}.");
        Prompt.ProcessedPrompt processedPrompt1 = new(prompt1);
        (ValidationResult validation1, ISentence sentence1, object result1) = engine.Run(processedPrompt1);

        if (validation1.IsValid)
        {
            Console.WriteLine("✓ Sentence validated successfully!");
            Console.WriteLine($"  Root word type: {sentence1?.Root?.GetType().Name ?? "null"}");

            // Display the result
            if (result1 != null)
            {
                if (result1 is string[] lines)
                {
                    Console.WriteLine($"  Execution result ({lines.Length} lines):");
                    foreach (string line in lines)
                    {
                        Console.WriteLine($"    {line}");
                    }
                }
                else
                {
                    Console.WriteLine($"  Execution result: {result1}");
                }
            }
            else
            {
                Console.WriteLine("  Execution result: null");
            }
        }
        else
        {
            Console.WriteLine($"✗ Validation failed: {validation1.FailureReason}");
        }

        Console.WriteLine();

        // Example 2: SAVE sentence
        Console.WriteLine("Example 2: SAVE sentence validation");
        Console.WriteLine("-------------------------------------");

        string prompt2 = "SAVE [data] TO output.txt.";
        Console.WriteLine($"Prompt: {prompt2}");
        Prompt.ProcessedPrompt processedPrompt2 = new(prompt2); (ValidationResult validation2, ISentence sentence2, object result2) = engine.Run(processedPrompt2);

        if (validation2.IsValid)
        {
            Console.WriteLine("✓ Sentence validated successfully!");
            Console.WriteLine($"  Root word type: {sentence2?.Root?.GetType().Name ?? "null"}");
            Console.WriteLine($"  Execution result: {result2 ?? "null"}");
        }
        else
        {
            Console.WriteLine($"✗ Validation failed: {validation2.FailureReason}");
        }

        Console.WriteLine();

        // Example 3: POST sentence
        Console.WriteLine("Example 3: POST sentence validation");
        Console.WriteLine("-------------------------------------");

        string prompt3 = "POST [data] TO endpoint.";
        Console.WriteLine($"Prompt: {prompt3}");
        Prompt.ProcessedPrompt processedPrompt3 = new(prompt3); (ValidationResult validation3, ISentence sentence3, object result3) = engine.Run(processedPrompt3);

        if (validation3.IsValid)
        {
            Console.WriteLine("✓ Sentence validated successfully!");
            Console.WriteLine($"  Root word type: {sentence3?.Root?.GetType().Name ?? "null"}");
            Console.WriteLine($"  Execution result: {result3 ?? "null"}");
        }
        else
        {
            Console.WriteLine($"✗ Validation failed: {validation3.FailureReason}");
        }

        Console.WriteLine();

        // Example 4: With variables
        Console.WriteLine("Example 4: Using variables");
        Console.WriteLine("---------------------------");

        engine.RegisterVariable("myData", "Hello FluNET!");
        engine.RegisterVariable("outputPath", "C:\\temp\\output.txt");
        Console.WriteLine("Registered variables: [myData], [outputPath]");

        string prompt4 = "SAVE [myData] TO output.txt.";
        Console.WriteLine($"Prompt: {prompt4}");
        Console.WriteLine("(Using resolved variable [myData] and literal path)");
        Prompt.ProcessedPrompt processedPrompt4 = new(prompt4); (ValidationResult validation4, ISentence sentence4, object result4) = engine.Run(processedPrompt4);

        if (validation4.IsValid)
        {
            Console.WriteLine("✓ Sentence validated successfully!");
            Console.WriteLine($"  Variable resolution: {result4 ?? "null"}");
        }
        else
        {
            Console.WriteLine($"✗ Validation failed: {validation4.FailureReason}");
        }

        Console.WriteLine("\n=== Demo Complete ===");
        Console.WriteLine("\nNote: Actual execution requires verb implementations to be");
        Console.WriteLine("registered in the WordFactory discovery system.");
    }
}
