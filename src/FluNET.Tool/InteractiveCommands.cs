using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Planning;
using FluNET.Prompt.Surface;

public static partial class FluNetTool
{
    private static async Task<int> InteractiveAsync()
    {
        using FluNETContext context = CreateSurfaceContext();
        Console.WriteLine("FluNET interactive session");
        Console.WriteLine(
            "Type :help for help, :begin/:end for multiline input, :paste for clipboard blocks, :quit to exit.");

        bool collecting = false;
        List<string> block = [];
        while (true)
        {
            Console.Write(collecting ? "......> " : "flunet> ");
            string? line = await Console.In.ReadLineAsync();
            if (line is null)
            {
                Console.WriteLine();
                return 0;
            }

            string input = line.Trim();
            if (collecting)
            {
                if (input.Equals(":end", StringComparison.OrdinalIgnoreCase))
                {
                    collecting = false;
                    input = string.Join(Environment.NewLine, block);
                    block.Clear();
                }
                else if (input.Equals(":cancel", StringComparison.OrdinalIgnoreCase))
                {
                    collecting = false;
                    block.Clear();
                    Console.WriteLine("Block cancelled.");
                    continue;
                }
                else
                {
                    block.Add(line);
                    continue;
                }
            }

            if (input.Length == 0)
                continue;
            if (input is ":quit" or ":exit" or "quit" or "exit")
                return 0;

            if (input.Equals(":help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    ":begin/:end multiline | :paste clipboard | :capabilities | :check PROMPT | " +
                    ":dry-run PROMPT | :explain PROMPT | :graph PROMPT | :fmt PROMPT | :quit");
                continue;
            }

            if (input.Equals(":begin", StringComparison.OrdinalIgnoreCase))
            {
                collecting = true;
                block.Clear();
                Console.WriteLine("Paste the block, then type :end.");
                continue;
            }

            if (input.Equals(":paste", StringComparison.OrdinalIgnoreCase))
            {
                string? clipboard = await context.GetService<IFluNetClipboard>().ReadTextAsync();
                if (!string.IsNullOrWhiteSpace(clipboard))
                {
                    int lines = clipboard.Replace("\r\n", "\n", StringComparison.Ordinal).Count(ch => ch == '\n') + 1;
                    Console.WriteLine($"Executing {lines} clipboard line(s)...");
                    await ExecuteInteractiveSourceAsync(context, clipboard);
                }
                else
                {
                    collecting = true;
                    block.Clear();
                    Console.WriteLine("Clipboard is empty or unavailable. Paste the block, then type :end.");
                }
                continue;
            }

            if (input.StartsWith(":check ", StringComparison.OrdinalIgnoreCase))
            {
                SurfaceCompilationResult result = context.CompileSurface(input[7..].Trim());
                if (result.IsValid)
                    Console.WriteLine($"Valid ({result.Plan!.Steps.Count} step(s)).");
                else
                    PrintDiagnostics(result);
                continue;
            }

            if (input.StartsWith(":dry-run ", StringComparison.OrdinalIgnoreCase))
            {
                SurfaceCompilationResult result = context.CompileSurface(input[9..].Trim());
                if (!result.IsValid)
                {
                    PrintDiagnostics(result);
                    continue;
                }

                foreach (ExecutionPlanStep step in result.Plan!.Steps)
                    Console.WriteLine($"PLAN {step.Index}: {step.Command.Frame.Id.Value}");
                Console.WriteLine("Dry run: no effects were executed.");
                continue;
            }

            if (input.StartsWith(":explain ", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    new SurfaceExplainService(context.GetSurfaceCompiler())
                        .Explain(input[9..].Trim())
                        .Text);
                continue;
            }

            if (input.StartsWith(":graph ", StringComparison.OrdinalIgnoreCase))
            {
                SurfaceCompilationResult result = context.CompileSurface(input[7..].Trim());
                if (result.IsValid)
                    Console.WriteLine(new SurfaceGraphExporter().ToDot(result));
                else
                    PrintDiagnostics(result);
                continue;
            }

            if (input.StartsWith(":fmt ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Console.WriteLine(new SurfaceFormatter().Format(input[5..].Trim()));
                }
                catch (FormatException exception)
                {
                    Console.Error.WriteLine(exception.Message);
                }
                continue;
            }

            await ExecuteInteractiveSourceAsync(context, input);
        }
    }

    private static async Task ExecuteInteractiveSourceAsync(FluNETContext context, string source)
    {
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(source);
        if (!execution.Compilation.IsValid)
        {
            PrintDiagnostics(execution.Compilation);
            return;
        }
        if (execution.Error is not null)
        {
            Console.Error.WriteLine(execution.Error.Message);
            return;
        }
        if (execution.Result is not null)
            Console.WriteLine(Format(execution.Result));
    }
}
