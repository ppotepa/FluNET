using FluNET;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddScoped<DiscoveryService>();
services.AddScoped<Engine>();
services.AddScoped<FluNET.Tokens.Tree.TokenTreeFactory>();
services.AddScoped<FluNET.Tokens.TokenFactory>();
services.AddScoped<FluNET.Lexicon.Lexicon>();
services.AddScoped<FluNET.Words.WordFactory>();
services.AddScoped<FluNET.Syntax.SentenceValidator>();
services.AddScoped<FluNET.Sentences.SentenceFactory>();
services.AddScoped<FluNET.Variables.VariableResolver>();
services.AddScoped<FluNET.Sentences.SentenceExecutor>();

var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<Engine>();

var testFile = @\"C:\Temp\test.txt\";
var prompt = new ProcessedPrompt($\"GET [text] FROM {testFile}.\"");
Console.WriteLine($\"Prompt: {prompt.Value}\"");

var (validation, sentence, result) = engine.Run(prompt);
Console.WriteLine($\"Validation: {validation.IsValid}\"");
if (!validation.IsValid) Console.WriteLine($\"Error: {validation.Message}\"");
