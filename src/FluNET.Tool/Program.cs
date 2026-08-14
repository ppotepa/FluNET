using FluNET.Automation;
using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Declarative;
using FluNET.Declarative.Reconciliation;
using FluNET.Execution;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using FluNET.Language;
using FluNET.Language.Contracts;
using FluNET.Persistence;
using FluNET.Prompt;
using FluNET.Security;
using FluNET.Tooling;
using System.Text.Json;

return await FluNetTool.RunAsync(args);

internal static class FluNetTool
{
    public static async Task<int> RunAsync(string[] args)
    {
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
        if (args.Count < 2) return UsageError("Usage: flunet exec \"CANONICAL PROMPT\"");
        string source = string.Join(' ', args.Skip(1));
        using FluNETContext context = FluNETContext.Create();
        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(source));
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine($"{result.Error!.Code} [{result.Error.Kind}]: {result.Error.Message}");
            return 5;
        }
        if (result.Result is not null) Console.WriteLine(Format(result.Result));
        return 0;
    }

    private static async Task<int> SurfaceAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 2) return UsageError($"Usage: flunet {args[0]} FILE");
        string source = await ReadSourceAsync(args[1]);
        string command = args[0].ToLowerInvariant();
        if (command == "fmt")
        {
            Console.WriteLine(new SurfaceFormatter().Format(source));
            return 0;
        }

        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompiler compiler = context.GetSurfaceCompiler();
        if (command == "check")
        {
            SurfaceCompilationResult compilation = compiler.Compile(new FluNET.Prompt.Surface.SourceDocument(source));
            if (!compilation.IsValid) return PrintDiagnostics(compilation);
            Console.WriteLine($"Valid: {compilation.Plan!.Steps.Count} step(s).");
            return 0;
        }
        if (command == "explain")
        {
            SurfaceExplanation explanation = new SurfaceExplainService(compiler).Explain(source);
            Console.WriteLine(explanation.Text);
            return explanation.Compilation.IsValid ? 0 : 3;
        }
        if (command == "graph")
        {
            SurfaceCompilationResult compilation = compiler.Compile(new FluNET.Prompt.Surface.SourceDocument(source));
            if (!compilation.IsValid) return PrintDiagnostics(compilation);
            Console.WriteLine(new SurfaceGraphExporter().ToDot(compilation));
            return 0;
        }

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(source);
        if (!execution.Compilation.IsValid) return PrintDiagnostics(execution.Compilation);
        if (execution.Error is not null) { Console.Error.WriteLine(execution.Error.Message); return 5; }
        if (execution.Result is not null) Console.WriteLine(Format(execution.Result));
        return 0;
    }

    private static async Task<int> CapabilitiesAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 2) return UsageError("Usage: flunet capabilities FILE");
        string source = await ReadSourceAsync(args[1]);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(source);
        if (!compilation.IsValid) return PrintDiagnostics(compilation);
        SurfaceSecurityManifest manifest = new SurfaceSecurityAnalyzer().Analyze(compilation);
        foreach (FluNetCapability capability in manifest.RequiredCapabilities)
            Console.WriteLine(capability);
        return 0;
    }

    private static async Task<int> AutomationAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3) return UsageError("Usage: flunet automation check|run|tick|signal FILE [args]");
        string action = args[1].ToLowerInvariant();
        string source = await ReadSourceAsync(args[2]);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        AutomationCompilationResult compilation = context.CompileAutomations(source);
        if (!compilation.IsValid)
        {
            foreach (AutomationDiagnostic diagnostic in compilation.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 3;
        }
        if (action == "check")
        {
            Console.WriteLine($"Valid: {compilation.Automations.Count} automation(s).");
            return 0;
        }
        if (action == "run")
        {
            int failed = 0;
            foreach (AutomationDefinition automation in compilation.Automations)
            {
                List<ExecutionStepResult> steps = [];
                try
                {
                    await context.GetService<ExecutionPlanExecutor>().ExecuteAsync(automation.Template.Compilation.Plan!, steps);
                    Console.WriteLine($"{automation.Id}: succeeded");
                }
                catch (Exception error) { failed++; Console.Error.WriteLine($"{automation.Id}: {error.Message}"); }
            }
            return failed == 0 ? 0 : 5;
        }
        if (action == "tick")
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string state = Path.Combine(Directory.GetCurrentDirectory(), ".flunet", "automation-schedule.json");
            for (int i = 3; i < args.Count; i++)
            {
                if (args[i] == "--at" && ++i < args.Count && DateTimeOffset.TryParse(args[i], out DateTimeOffset parsed)) now = parsed;
                else if (args[i] == "--state" && ++i < args.Count) state = args[i];
                else return UsageError($"Unknown tick argument '{args[i]}'.");
            }
            AutomationScheduler scheduler = new(
                context.GetService<ExecutionPlanExecutor>(),
                new DurableAutomationScheduleStore(state, context.GetService<IExecutionPolicy>()));
            foreach (AutomationDefinition automation in compilation.Automations) await scheduler.RegisterAsync(automation, now);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.TickAsync(now);
            foreach (AutomationRunResult run in runs) Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        if (action == "signal")
        {
            if (args.Count < 4) return UsageError("Usage: flunet automation signal FILE RESOURCE [EVENT]");
            AutomationScheduler scheduler = new(context.GetService<ExecutionPlanExecutor>(), new InMemoryAutomationScheduleStore());
            foreach (AutomationDefinition automation in compilation.Automations) await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.PublishSignalAsync(args[3], args.Count > 4 ? args[4] : null);
            foreach (AutomationRunResult run in runs) Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        return UsageError($"Unknown automation action '{action}'.");
    }

    private static async Task<int> EnsureAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3) return UsageError("Usage: flunet ensure check|apply FILE");
        string source = await ReadSourceAsync(args[2]);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        DesiredStateCompilationResult compilation = context.CompileEnsure(source);
        if (!compilation.IsValid)
        {
            foreach (DesiredStateDiagnostic diagnostic in compilation.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 3;
        }
        if (args[1].Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Valid: {compilation.Plans.Count} ENSURE goal(s).");
            return 0;
        }
        if (args[1].Equals("apply", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<EnsureRunResult> runs = await context.ExecuteEnsureAsync(source);
            foreach (EnsureRunResult run in runs) Console.WriteLine($"{run.Plan.Goal.Target}: {(run.IsSuccess ? "satisfied" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        return UsageError($"Unknown ensure action '{args[1]}'.");
    }

    private static async Task<int> SyncAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3) return UsageError("Usage: flunet sync check|apply FILE");
        string source = await ReadSourceAsync(args[2]);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncCompilationResult compilation = context.CompileSync(source);
        if (!compilation.IsValid)
        {
            foreach (SyncDiagnostic diagnostic in compilation.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 3;
        }
        if (args[1].Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Valid: {compilation.Definitions.Count} SYNC definition(s).");
            return 0;
        }
        if (args[1].Equals("apply", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<ReconciliationRunResult> runs = await context.ExecuteSyncAsync(source);
            foreach (ReconciliationRunResult run in runs)
            {
                string status = run.IsSuccess ? (run.Applied ? "applied" : "unchanged") : "failed";
                Console.WriteLine($"{run.Definition.Goal.TargetResource}: {status}");
                if (run.Diff is not null)
                    Console.WriteLine($"  create={run.Diff.Creates} update={run.Diff.Updates} delete={run.Diff.Deletes} conflict={run.Diff.Conflicts}");
            }
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        return UsageError($"Unknown sync action '{args[1]}'.");
    }

    private static async Task<int> HistoryAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3) return UsageError("Usage: flunet history list|show DIRECTORY [RUN_ID]");
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

    private static async Task<string> ReadSourceAsync(string path) =>
        path == "-" ? await Console.In.ReadToEndAsync() : await File.ReadAllTextAsync(path);

    private static int PrintDiagnostics(SurfaceCompilationResult result)
    {
        foreach (var diagnostic in result.Lowering.Diagnostics)
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        foreach (CompilationDiagnostic diagnostic in result.Diagnostics)
            Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.Phase}]: {diagnostic.Message}");
        return 3;
    }

    private static string Format(object value) => value switch
    {
        string text => text,
        string[] lines => string.Join(Environment.NewLine, lines),
        _ => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true })
    };

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("Run `flunet --help` for usage.");
        return 64;
    }

    private static void Help() => Console.WriteLine("""
FluNET 1.0-source-candidate tool

  flunet version
  flunet contract
  flunet exec "CANONICAL PROMPT"
  flunet check|fmt|explain|graph|run FILE
  flunet capabilities FILE
  flunet automation check|run|tick|signal FILE ...
  flunet ensure check|apply FILE
  flunet sync check|apply FILE
  flunet history list DIRECTORY
  flunet history show DIRECTORY RUN_ID
  flunet persistence

Use FILE `-` where supported to read source from stdin.
""");
}
