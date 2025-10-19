using FluNET;
using FluNET.Sentences;
using FluNET.Syntax;
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

        // Note: Terminator must be attached to last token due to tokenizer design
        string prompt1 = "GET [text] FROM [file.txt].";
        Console.WriteLine($"Prompt: {prompt1}");
        Prompt.ProcessedPrompt processedPrompt1 = new(prompt1);
        var (validation1, sentence1, result1) = engine.Run(processedPrompt1);

        if (validation1.IsValid)
        {
            Console.WriteLine("✓ Sentence validated successfully!");
            Console.WriteLine($"  Root word type: {sentence1?.Root?.GetType().Name ?? "null"}");
            Console.WriteLine($"  Execution result: {result1 ?? "null"}");
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
        Prompt.ProcessedPrompt processedPrompt2 = new(prompt2); var (validation2, sentence2, result2) = engine.Run(processedPrompt2);

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
        Prompt.ProcessedPrompt processedPrompt3 = new(prompt3); var (validation3, sentence3, result3) = engine.Run(processedPrompt3);

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
        Prompt.ProcessedPrompt processedPrompt4 = new(prompt4); var (validation4, sentence4, result4) = engine.Run(processedPrompt4);

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
