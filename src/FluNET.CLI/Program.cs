using FluNET.Token;
using FluNET.TokenTree;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.CLI;

internal class Program
{
    private static void Main(string[] args)
    {
        var prompt = "GET [DATA] FROM https://api.example.com/data AND SAVE TO [file.txt]";
        var processedPrompt = new Prompt.ProcessedPrompt(prompt);

        IServiceCollection serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton<Engine>();
        serviceCollection.AddScoped<TokenTreeFactory>();
        serviceCollection.AddScoped<TokenFactory>();

        var provider = serviceCollection.BuildServiceProvider();
        var engine = provider.GetRequiredService<Engine>();

        var result = engine.Run(processedPrompt);

    }
}
