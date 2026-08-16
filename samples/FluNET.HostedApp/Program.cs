using System.Text.Json;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string prompt = args.Length > 1
    ? string.Join(' ', args.Skip(1))
    : "CAPABILITIES [caps]";

using FluNETContext context = FluNetHost.Create(new FluNetHostOptions
{
    Root = root,
    DataDirectory = Path.Combine(root, ".flunet")
});

ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(prompt));
if (!result.IsSuccess)
{
    Console.Error.WriteLine($"{result.Error!.Code}: {result.Error.Message}");
    return 1;
}

if (result.Result is not null)
    Console.WriteLine(JsonSerializer.Serialize(result.Result, new JsonSerializerOptions { WriteIndented = true }));
return 0;
