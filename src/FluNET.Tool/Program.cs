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
using FluNET.Prompt.Surface;
using FluNET.Security;
using FluNET.Telemetry;
using FluNET.Tool;
using FluNET.Tooling;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

return await FluNetTool.RunAsync(args);

public static class FluNetTool
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
            else if (command == "run" && argument.Equals("--verbosity", StringComparison.OrdinalIgnoreCase) && ++index < args.Count &&
                int.TryParse(args[index], out int explicitLevel) && explicitLevel is >= 0 and <= 3)
            {
                verbosity = explicitLevel;
            }
            else if (sourceArgument is null && (!argument.StartsWith('-') || argument == "-"))
            {
                sourceArgument = argument;
            }
            else if (argument.Equals("--queue", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                queuePath = args[index];
            else if (argument.Equals("--store", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                storePath = args[index];
            else if (argument.Equals("--blob", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                blobPath = args[index];
            else return UsageError($"Unknown surface argument '{argument}'.");
        }
        if (sourceArgument is null) return UsageError($"Usage: flunet {command} FILE");
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
                !string.Equals(Path.GetFullPath(Directory.GetCurrentDirectory()), executionDirectory, StringComparison.OrdinalIgnoreCase))
            {
                originalDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(executionDirectory);
            }
        }

        try
        {
            using FluNETContext context = CreateSurfaceContext(queuePath, storePath, blobPath, command == "run" ? verbosity : 0);
            SourceDocument document = new(source, SourceSyntaxKind.Auto, sourcePath);
            SurfaceCompiler compiler = context.GetSurfaceCompiler();
            if (command == "check")
            {
                SurfaceCompilationResult compilation = compiler.Compile(document);
                if (!compilation.IsValid) return PrintDiagnostics(compilation);
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
                if (!compilation.IsValid) return PrintDiagnostics(compilation);
                Console.WriteLine(new SurfaceGraphExporter().ToDot(compilation));
                return 0;
            }

            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(document);
            if (!execution.Compilation.IsValid) return PrintDiagnostics(execution.Compilation);
            if (command == "run" && verbosity > 0) PrintRunDiagnostics(execution, verbosity);
            if (execution.Error is not null) { Console.Error.WriteLine(execution.Error.Message); return 5; }
            if (execution.Result is not null) Console.WriteLine(Format(execution.Result));
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
        if (argument.Length < 2 || argument[0] != '-' || argument[1] != 'v' ||
            argument.Skip(1).Any(character => character != 'v'))
            return false;

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
                Console.Error.WriteLine($"[plan] #{step.Index} {step.Command.Command.Name}/{step.Command.Frame.UsageName} <- {dependencies}");
                Console.Error.WriteLine($"       {sentence}");
            }
        }

        foreach (ExecutionStepResult result in execution.Steps.OrderBy(item => item.Step.Index))
        {
            if (verbosity >= 1)
                Console.Error.WriteLine($"[run] #{result.Step.Index} {result.Status} {result.Step.Command.Command.Name}/{result.Step.Command.Frame.UsageName} (attempts: {result.Attempts})");
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

    private static async Task<int> AutomationAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3) return UsageError("Usage: flunet automation check|run|tick|daemon|signal|replay FILE [args]");
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
                    await context.GetService<SentenceExecutor>().ExecuteAsync(automation.Template.Compilation.Plan!, steps);
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
                context.GetService<SentenceExecutor>(),
                new DurableAutomationScheduleStore(state, context.GetService<IExecutionPolicy>()));
            foreach (AutomationDefinition automation in compilation.Automations) await scheduler.RegisterAsync(automation, now);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.TickAsync(now);
            foreach (AutomationRunResult run in runs) Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        if (action == "daemon")
            return await AutomationDaemonAsync(args, context, compilation);
        if (action == "signal")
        {
            if (args.Count < 4) return UsageError("Usage: flunet automation signal FILE RESOURCE [EVENT]");
            AutomationScheduler scheduler = new(context.GetService<SentenceExecutor>(), new InMemoryAutomationScheduleStore());
            foreach (AutomationDefinition automation in compilation.Automations) await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.PublishSignalAsync(args[3], args.Count > 4 ? args[4] : null);
            foreach (AutomationRunResult run in runs) Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        if (action == "replay")
        {
            if (args.Count < 4) return UsageError("Usage: flunet automation replay FILE EVENTS_PATH [--event NAME]");
            string eventsPath = args[3];
            string? eventName = null;
            for (int index = 4; index < args.Count; index++)
            {
                if (args[index].Equals("--event", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                    eventName = args[index];
                else return UsageError($"Unknown replay argument '{args[index]}'.");
            }

            AutomationScheduler scheduler = new(context.GetService<SentenceExecutor>(), new InMemoryAutomationScheduleStore());
            foreach (AutomationDefinition automation in compilation.Automations)
                await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);
            IAutomationSignalStore signalStore = CreateSignalStore(eventsPath, context.GetService<IExecutionPolicy>());
            IReadOnlyList<AutomationRunResult> runs = await scheduler.ReplaySignalsAsync(signalStore, eventName);
            foreach (AutomationRunResult run in runs)
                Console.WriteLine($"{run.Signal?.EventName}: {run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        if (action == "watch")
        {
            if (args.Count < 5)
                return UsageError("Usage: flunet automation watch FILE DIRECTORY RESOURCE [--filter PATTERN] [--recursive] [--events PATH]");

            string directory = args[3];
            string resource = args[4];
            string filter = "*";
            bool recursive = false;
            string? eventsPath = null;
            for (int index = 5; index < args.Count; index++)
            {
                if (args[index].Equals("--recursive", StringComparison.OrdinalIgnoreCase)) recursive = true;
                else if (args[index].Equals("--filter", StringComparison.OrdinalIgnoreCase) && ++index < args.Count) filter = args[index];
                else if (args[index].Equals("--events", StringComparison.OrdinalIgnoreCase) && ++index < args.Count) eventsPath = args[index];
                else return UsageError($"Unknown watch argument '{args[index]}'.");
            }

            AutomationScheduler scheduler = new(context.GetService<SentenceExecutor>(), new InMemoryAutomationScheduleStore());
            foreach (AutomationDefinition automation in compilation.Automations)
                await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);

            using CancellationTokenSource cancellation = new();
            ConsoleCancelEventHandler handler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += handler;
            try
            {
                IAutomationSignalStore? signalStore = eventsPath is null
                    ? null
                    : CreateSignalStore(eventsPath, context.GetService<IExecutionPolicy>());
                FileWatchAutomationBridge bridge = new(context.GetService<IFluNetFileWatcher>(), scheduler, signalStore);
                await bridge.RunAsync(
                    directory,
                    resource,
                    filter,
                    recursive,
                    cancellation.Token,
                    (change, runs) =>
                    {
                        foreach (AutomationRunResult run in runs)
                            Console.WriteLine($"{change.Kind}: {run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
                        return ValueTask.CompletedTask;
                    });
            }
            finally { Console.CancelKeyPress -= handler; }
            return 0;
        }
        return UsageError($"Unknown automation action '{action}'.");
    }

    private static async Task<int> AutomationDaemonAsync(
        IReadOnlyList<string> args,
        FluNETContext context,
        AutomationCompilationResult compilation)
    {
        string state = Path.Combine(Directory.GetCurrentDirectory(), ".flunet", "automation-schedule.json");
        TimeSpan poll = TimeSpan.FromSeconds(1);
        for (int index = 3; index < args.Count; index++)
        {
            if (args[index].Equals("--state", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                state = args[index];
            else if (args[index].Equals("--interval", StringComparison.OrdinalIgnoreCase) && ++index < args.Count &&
                     TryAutomationDuration(args[index], out TimeSpan parsed))
                poll = parsed;
            else
                return UsageError("Usage: flunet automation daemon FILE [--state PATH] [--interval 1s]");
        }

        AutomationScheduler scheduler = new(
            context.GetService<SentenceExecutor>(),
            new DurableAutomationScheduleStore(state, context.GetService<IExecutionPolicy>()));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (AutomationDefinition automation in compilation.Automations)
            await scheduler.RegisterAsync(automation, now);

        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            Console.WriteLine($"Running {compilation.Automations.Count} automation(s). Press Ctrl+C to stop.");
            while (!cancellation.IsCancellationRequested)
            {
                IReadOnlyList<AutomationRunResult> runs = await scheduler.TickAsync(DateTimeOffset.UtcNow, cancellation.Token);
                foreach (AutomationRunResult run in runs)
                    Console.WriteLine($"{DateTimeOffset.UtcNow:O} {run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
                await Task.Delay(poll, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { Console.CancelKeyPress -= handler; }
        return 0;
    }

    private static bool TryAutomationDuration(string value, out TimeSpan duration)
    {
        string text = value.Trim().ToLowerInvariant();
        double multiplier = text.EndsWith("ms", StringComparison.Ordinal) ? 0.001 :
            text.EndsWith('s') ? 1 : text.EndsWith('m') ? 60 : text.EndsWith('h') ? 3600 : 0;
        string number = multiplier == 0 ? string.Empty : text[..^(text.EndsWith("ms", StringComparison.Ordinal) ? 2 : 1)];
        if (!double.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) ||
            parsed <= 0 || parsed > 3600)
        {
            duration = default;
            return false;
        }
        duration = TimeSpan.FromSeconds(parsed * multiplier);
        return duration >= TimeSpan.FromMilliseconds(50);
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

    private static async Task<int> InteractiveAsync()
    {
        using FluNETContext context = CreateSurfaceContext();
        Console.WriteLine("FluNET interactive session");
        Console.WriteLine("Type :help for help, :begin/:end for multiline input, :paste for clipboard blocks, :quit to exit.");

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
                Console.WriteLine(":begin/:end multiline | :paste clipboard | :capabilities | :check PROMPT | :dry-run PROMPT | :explain PROMPT | :graph PROMPT | :fmt PROMPT | :quit");
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
                if (result.IsValid) Console.WriteLine($"Valid ({result.Plan!.Steps.Count} step(s)).");
                else PrintDiagnostics(result);
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
                Console.WriteLine(new SurfaceExplainService(context.GetSurfaceCompiler()).Explain(input[9..].Trim()).Text);
                continue;
            }
            if (input.StartsWith(":graph ", StringComparison.OrdinalIgnoreCase))
            {
                SurfaceCompilationResult result = context.CompileSurface(input[7..].Trim());
                if (result.IsValid) Console.WriteLine(new SurfaceGraphExporter().ToDot(result));
                else PrintDiagnostics(result);
                continue;
            }
            if (input.StartsWith(":fmt ", StringComparison.OrdinalIgnoreCase))
            {
                try { Console.WriteLine(new SurfaceFormatter().Format(input[5..].Trim())); }
                catch (FormatException exception) { Console.Error.WriteLine(exception.Message); }
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
                services.AddSingleton<IFluNetMessageBus>(provider =>
                    CreateMessageBus(queuePath, provider.GetRequiredService<IExecutionPolicy>()));
            if (storePath is not null)
                services.AddSingleton<IFluNetKeyValueStore>(provider =>
                    CreateKeyValueStore(storePath, provider.GetRequiredService<IExecutionPolicy>()));
            if (blobPath is not null)
                services.AddSingleton<IFluNetBlobStore>(provider =>
                    new FileFluNetBlobStore(
                        blobPath,
                        provider.GetRequiredService<IExecutionPolicy>()));
        });

    private static async Task<string> ReadSourceAsync(string path) =>
        path == "-" ? await Console.In.ReadToEndAsync() : await File.ReadAllTextAsync(path);

    private static IAutomationSignalStore CreateSignalStore(string path, IExecutionPolicy policy) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteAutomationSignalStore(path, policy)
            : new JsonFileAutomationSignalStore(path, policy);

    private static IFluNetKeyValueStore CreateKeyValueStore(string path, IExecutionPolicy policy) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteFluNetKeyValueStore(path, policy)
            : new JsonFileFluNetKeyValueStore(path, policy);

    private static IFluNetMessageBus CreateMessageBus(string path, IExecutionPolicy policy) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteFluNetMessageBus(path, policy)
            : new JsonFileFluNetMessageBus(path, policy);

    private static int PrintDiagnostics(SurfaceCompilationResult result)
    {
        foreach (var diagnostic in result.Lowering.Diagnostics)
            Console.Error.WriteLine($"{diagnostic.Code} ({Location(result.Document.Text, diagnostic.Span)}): {diagnostic.Message}");
        foreach (CompilationDiagnostic diagnostic in result.Diagnostics)
            Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.Phase}] ({Location(result.Document.Text, diagnostic.Span)}): {diagnostic.Message}");
        return 3;
    }

    private static string Location(string source, FluNET.Prompt.SourceSpan span)
    {
        int bounded = Math.Clamp(span.Start, 0, source.Length);
        int line = 1;
        int column = 1;
        for (int index = 0; index < bounded; index++)
        {
            if (source[index] == '\n') { line++; column = 1; }
            else column++;
        }
        return $"line {line}, column {column}";
    }

    private static string Format(object value) => value switch
    {
        FileSystemInfo fileSystemInfo => fileSystemInfo.FullName,
        Uri uri => uri.AbsoluteUri,
        string text => text,
        string[] lines => string.Join(Environment.NewLine, lines),
        JsonElement element => JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true }),
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
