using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Planning;
using FluNET.Prompt;
using FluNET.Prompt.Surface;
using FluNET.Telemetry;
using FluNET.Tool;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

public static partial class FluNetTool
{
    private static async Task<int> SurfaceAsync(IReadOnlyList<string> args)
    {
        string command = args[0].ToLowerInvariant();
        string? sourceArgument = null;
        int verbosity = 0;
        string? queuePath = null;
        string? storePath = null;
        string? blobPath = null;
        for (int index = 1; index < args.Count; index++)
        {
            string argument = args[index];
            if (command == "run" && TryReadVerbosity(argument, out int level))
            {
                verbosity = Math.Max(verbosity, level);
            }
            else if (command == "run" &&
                     argument.Equals("--verbosity", StringComparison.OrdinalIgnoreCase) &&
                     ++index < args.Count &&
                     int.TryParse(args[index], out int explicitLevel) &&
                     explicitLevel is >= 0 and <= 3)
            {
                verbosity = explicitLevel;
            }
            else if (sourceArgument is null && (!argument.StartsWith('-') || argument == "-"))
            {
                sourceArgument = argument;
            }
            else if (argument.Equals("--queue", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
            {
                queuePath = args[index];
            }
            else if (argument.Equals("--store", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
            {
                storePath = args[index];
            }
            else if (argument.Equals("--blob", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
            {
                blobPath = args[index];
            }
            else
            {
                return UsageError($"Unknown surface argument '{argument}'.");
            }
        }

        if (sourceArgument is null)
            return UsageError($"Usage: flunet {command} FILE");

        string source = await ReadSourceAsync(sourceArgument);
        if (command == "fmt")
        {
            Console.WriteLine(new SurfaceFormatter().Format(source));
            return 0;
        }

        string? sourcePath = sourceArgument == "-" ? null : Path.GetFullPath(sourceArgument);
        string? originalDirectory = null;
        if (command == "run" && sourceArgument != "-")
        {
            string? executionDirectory = FindExecutionDirectory(sourcePath!);
            if (executionDirectory is not null &&
                !string.Equals(
                    Path.GetFullPath(Directory.GetCurrentDirectory()),
                    executionDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                originalDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(executionDirectory);
            }
        }

        try
        {
            using FluNETContext context = CreateSurfaceContext(
                queuePath,
                storePath,
                blobPath,
                command == "run" ? verbosity : 0);
            SourceDocument document = new(source, SourceSyntaxKind.Auto, sourcePath);
            SurfaceCompiler compiler = context.GetSurfaceCompiler();
            if (command == "check")
            {
                SurfaceCompilationResult compilation = compiler.Compile(document);
                if (!compilation.IsValid)
                    return PrintDiagnostics(compilation);
                Console.WriteLine($"Valid: {compilation.Plan!.Steps.Count} step(s).");
                return 0;
            }

            if (command == "explain")
            {
                SurfaceExplanation explanation = new SurfaceExplainService(compiler).Explain(document);
                Console.WriteLine(explanation.Text);
                return explanation.Compilation.IsValid ? 0 : 3;
            }

            if (command == "graph")
            {
                SurfaceCompilationResult compilation = compiler.Compile(document);
                if (!compilation.IsValid)
                    return PrintDiagnostics(compilation);
                Console.WriteLine(new SurfaceGraphExporter().ToDot(compilation));
                return 0;
            }

            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(document);
            if (!execution.Compilation.IsValid)
                return PrintDiagnostics(execution.Compilation);
            if (command == "run" && verbosity > 0)
                PrintRunDiagnostics(execution, verbosity);
            if (execution.Error is not null)
            {
                Console.Error.WriteLine(execution.Error.Message);
                return 5;
            }
            if (execution.Result is not null)
                Console.WriteLine(Format(execution.Result));
            return 0;
        }
        finally
        {
            if (originalDirectory is not null)
                Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private static string? FindExecutionDirectory(string sourcePath)
    {
        if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "fixtures")))
            return null;

        DirectoryInfo? candidate = new FileInfo(sourcePath).Directory;
        while (candidate is not null)
        {
            if (Directory.Exists(Path.Combine(candidate.FullName, "fixtures")))
                return candidate.FullName;
            candidate = candidate.Parent;
        }

        return null;
    }

    private static bool TryReadVerbosity(string argument, out int level)
    {
        level = 0;
        if (argument.Length < 2 ||
            argument[0] != '-' ||
            argument[1] != 'v' ||
            argument.Skip(1).Any(character => character != 'v'))
        {
            return false;
        }

        level = argument.Length - 1;
        return level <= 3;
    }

    private static void PrintRunDiagnostics(SurfaceExecutionResult execution, int verbosity)
    {
        SurfaceCompilationResult compilation = execution.Compilation;
        ExecutionPlan? plan = compilation.Plan;
        if (verbosity >= 2 && plan is not null)
        {
            Console.Error.WriteLine($"[run] plan: {plan.Steps.Count} step(s), {plan.Variables.Count} variable(s)");
            foreach (ExecutionPlanStep step in plan.Steps)
            {
                string dependencies = step.Dependencies.Count == 0
                    ? "independent"
                    : string.Join(", ", step.Dependencies.Select(item => $"#{item.PredecessorIndex} ({item.Kind})"));
                string sentence = compilation.Document.FindSentence(step.Command.Syntax.Span)?.Text ?? "<synthetic>";
                Console.Error.WriteLine(
                    $"[plan] #{step.Index} {step.Command.Command.Name}/{step.Command.Frame.UsageName} <- {dependencies}");
                Console.Error.WriteLine($"       {sentence}");
            }
        }

        foreach (ExecutionStepResult result in execution.Steps.OrderBy(item => item.Step.Index))
        {
            if (verbosity >= 1)
            {
                Console.Error.WriteLine(
                    $"[run] #{result.Step.Index} {result.Status} {result.Step.Command.Command.Name}/{result.Step.Command.Frame.UsageName} " +
                    $"(attempts: {result.Attempts})");
            }

            if (verbosity >= 3)
            {
                if (result.Error is not null)
                    Console.Error.WriteLine($"       error: {result.Error.Message}");
                if (result.Result is not null)
                    Console.Error.WriteLine($"       result: {DescribeResult(result.Result)}");
            }
        }

        if (verbosity >= 3 && execution.Error is not null)
            Console.Error.WriteLine($"[run] fatal: {execution.Error}");
    }

    private static FluNETContext CreateSurfaceContext(
        string? queuePath = null,
        string? storePath = null,
        string? blobPath = null,
        int verbosity = 0) =>
        SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            if (verbosity > 0)
                services.AddSingleton<IFluNetTelemetrySink>(new ConsoleFluNetTelemetrySink(verbosity));
            if (queuePath is not null)
            {
                services.AddSingleton<IFluNetMessageBus>(provider =>
                    CreateMessageBus(queuePath, provider.GetRequiredService<IExecutionPolicy>()));
            }
            if (storePath is not null)
            {
                services.AddSingleton<IFluNetKeyValueStore>(provider =>
                    CreateKeyValueStore(storePath, provider.GetRequiredService<IExecutionPolicy>()));
            }
            if (blobPath is not null)
            {
                services.AddSingleton<IFluNetBlobStore>(provider =>
                    new FileFluNetBlobStore(
                        blobPath,
                        provider.GetRequiredService<IExecutionPolicy>()));
            }
        });

    private static async Task<string> ReadSourceAsync(string path) =>
        path == "-" ? await Console.In.ReadToEndAsync() : await File.ReadAllTextAsync(path);

    private static IFluNetKeyValueStore CreateKeyValueStore(string path, IExecutionPolicy policy) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteFluNetKeyValueStore(path, policy)
            : new JsonFileFluNetKeyValueStore(path, policy);

    private static IFluNetMessageBus CreateMessageBus(string path, IExecutionPolicy policy) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteFluNetMessageBus(path, policy)
            : new JsonFileFluNetMessageBus(path, policy);

    private static int PrintDiagnostics(SurfaceCompilationResult result)
    {
        foreach (SurfaceDiagnostic diagnostic in result.Lowering.Diagnostics)
        {
            Console.Error.WriteLine(
                $"{diagnostic.Code} ({Location(result.Document.Text, diagnostic.Span)}): {diagnostic.Message}");
        }
        foreach (CompilationDiagnostic diagnostic in result.Diagnostics)
        {
            Console.Error.WriteLine(
                $"{diagnostic.Code} [{diagnostic.Phase}] ({Location(result.Document.Text, diagnostic.Span)}): {diagnostic.Message}");
        }
        return 3;
    }

    private static string Location(string source, SourceSpan span)
    {
        int bounded = Math.Clamp(span.Start, 0, source.Length);
        int line = 1;
        int column = 1;
        for (int index = 0; index < bounded; index++)
        {
            if (source[index] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
        return $"line {line}, column {column}";
    }

    private static string Format(object value) => value switch
    {
        FileSystemInfo fileSystemInfo => fileSystemInfo.FullName,
        Uri uri => uri.AbsoluteUri,
        string text => text,
        string[] lines => string.Join(Environment.NewLine, lines),
        JsonElement element => JsonSerializer.Serialize(
            element,
            new JsonSerializerOptions { WriteIndented = true }),
        _ => JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        })
    };

    private static string DescribeResult(object value) => value switch
    {
        FileSystemInfo fileSystemInfo => $"{value.GetType().Name}(path={fileSystemInfo.FullName})",
        Uri uri => $"Uri(scheme={uri.Scheme}, host={uri.Host})",
        string text => $"String(length={text.Length})",
        JsonElement element => element.ValueKind switch
        {
            JsonValueKind.Array => $"JsonArray(count={element.GetArrayLength()})",
            JsonValueKind.Object => $"JsonObject(properties={element.EnumerateObject().Count()})",
            JsonValueKind.String => $"JsonString(length={element.GetString()?.Length ?? 0})",
            _ => $"Json{element.ValueKind}"
        },
        System.Collections.ICollection collection =>
            $"{value.GetType().Name}(count={collection.Count})",
        _ => value.GetType().Name
    };
}
