using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Execution.Workflow;
using FluNET.Language;
using FluNET.Language.Contracts;
using FluNET.Persistence;
using FluNET.Prompt;
using FluNET.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

return await FluNetTool.RunAsync(args);

public static partial class FluNetTool
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 && !Console.IsInputRedirected)
            return await InteractiveAsync();
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Help();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "version" => Version(),
                "contract" => Contract(),
                "exec" => await ExecAsync(args),
                "check" or "fmt" or "explain" or "graph" or "run" => await SurfaceAsync(args),
                "capabilities" => await CapabilitiesAsync(args),
                "tools" => Tools(args),
                "automation" => await AutomationAsync(args),
                "ensure" => await EnsureAsync(args),
                "sync" => await SyncAsync(args),
                "history" => await HistoryAsync(args),
                "persistence" => Persistence(),
                _ => UsageError($"Unknown command '{args[0]}'.")
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 5;
        }
    }

    private static int Version()
    {
        Console.WriteLine($"FluNET language identity {StandardLanguageIdentity.Version}");
        Console.WriteLine("platform contract 1.0-source-candidate (not release-verified)");
        return 0;
    }

    private static int Contract()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        LanguageContractManifest manifest = LanguageContractManifest.Create(
            context.GetService<LanguageSnapshot>(),
            StandardLanguageIdentity.Version);
        Console.WriteLine(manifest.ToJson());
        return 0;
    }

    private static async Task<int> ExecAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
            return UsageError("Usage: flunet exec \"CANONICAL PROMPT\"");

        string source = string.Join(' ', args.Skip(1));
        using FluNETContext context = FluNETContext.Create();
        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(source));
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine($"{result.Error!.Code} [{result.Error.Kind}]: {result.Error.Message}");
            return 5;
        }

        if (result.Result is not null)
            Console.WriteLine(Format(result.Result));
        return 0;
    }

    private static async Task<int> CapabilitiesAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
            return UsageError("Usage: flunet capabilities FILE");

        string source = await ReadSourceAsync(args[1]);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(source);
        if (!compilation.IsValid)
            return PrintDiagnostics(compilation);

        SurfaceSecurityManifest manifest = new SurfaceSecurityAnalyzer().Analyze(compilation);
        foreach (FluNetCapability capability in manifest.RequiredCapabilities)
            Console.WriteLine(capability);
        return 0;
    }

    private static int Tools(IReadOnlyList<string> args)
    {
        bool json = args.Skip(1).All(argument => argument.Equals("--json", StringComparison.OrdinalIgnoreCase));
        if (args.Skip(1).Any(argument => !argument.Equals("--json", StringComparison.OrdinalIgnoreCase)))
            return UsageError("Usage: flunet tools [--json]");

        using FluNETContext context = CreateSurfaceContext();
        CapabilityRegistry registry = context.GetService<CapabilityRegistry>();
        var capabilities = registry.Describe().Select(capability => new
        {
            capability.Id,
            capability.Version,
            Available = registry.TryResolve(capability.Id, out _),
            Platform = FluNetPlatformInfo.Current,
            capability.Platforms,
            capability.Permissions
        }).ToArray();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(capabilities, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            }));
            return 0;
        }

        foreach (var capability in capabilities)
        {
            Console.WriteLine($"{capability.Id} v{capability.Version} {(capability.Available ? "AVAILABLE" : "DENIED")} " +
                $"[{string.Join(", ", capability.Platforms)}] permissions: {string.Join(", ", capability.Permissions)}");
        }
        return 0;
    }

    private static async Task<int> HistoryAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3)
            return UsageError("Usage: flunet history list|show DIRECTORY [RUN_ID]");

        string action = args[1].ToLowerInvariant();
        string directory = args[2];
        IExecutionPolicy policy = new AllowAllExecutionPolicy();
        DurableWorkflowStoreOptions options = new(directory);
        WorkflowHistoryService history = new(
            new DurableWorkflowStateStore(options, policy),
            new DurableWorkflowRunCatalog(options, policy));

        if (action == "list")
        {
            IReadOnlyList<WorkflowRunSummary> runs = await history.ListAsync();
            foreach (WorkflowRunSummary run in runs)
                Console.WriteLine($"{run.RunId} {run.LastStatus} events={run.EventCount} updated={run.LastUpdatedAt:O}");
            return 0;
        }

        if (action == "show")
        {
            if (args.Count < 4 || !Guid.TryParse(args[3], out Guid runId))
                return UsageError("Usage: flunet history show DIRECTORY RUN_ID");
            WorkflowRunHistory run = await history.GetAsync(runId);
            Console.WriteLine(JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        return UsageError($"Unknown history action '{action}'.");
    }

    private static int Persistence()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        PersistenceContractManifest manifest = PersistenceContractInspector.Inspect(context.ServiceProvider);
        Console.WriteLine(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("Run `flunet --help` for usage.");
        return 64;
    }

    private static void Help() => Console.WriteLine("""
FluNET 1.0-source-candidate tool

  flunet                         interactive session (attached terminal)
  flunet version
  flunet contract
  flunet exec "CANONICAL PROMPT"
  flunet check|fmt|explain|graph|run FILE [--queue PATH] [--store PATH] [--blob PATH]
  flunet run [-v|-vv|-vvv] FILE [--queue PATH] [--store PATH] [--blob PATH]
  flunet capabilities FILE
  flunet tools
  flunet automation check|run|tick|daemon|signal|replay|watch FILE ...
  flunet ensure check|apply FILE
  flunet sync check|apply FILE
  flunet history list DIRECTORY
  flunet history show DIRECTORY RUN_ID
  flunet persistence

  flu run FILE [--queue PATH] [--store PATH] [--blob PATH]

Use FILE `-` where supported to read source from stdin.
Run verbosity: `-v` steps, `-vv` plan and dependencies, `-vvv` dispatch and safe result details. Flags may appear before or after FILE.
In the interactive session use `:begin`/`:end` for blocks, `:paste` for clipboard input, and `:check`, `:dry-run`, `:explain`, `:graph` or `:fmt` for inspection.
""");
}
