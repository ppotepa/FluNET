using FluNET.Automation;
using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution.Planning;

public static partial class FluNetTool
{
    private static async Task<int> AutomationAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3)
            return UsageError("Usage: flunet automation check|run|tick|daemon|signal|replay FILE [args]");

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
                    await context.GetService<SentenceExecutor>()
                        .ExecuteAsync(automation.Template.Compilation.Plan!, steps);
                    Console.WriteLine($"{automation.Id}: succeeded");
                }
                catch (Exception error)
                {
                    failed++;
                    Console.Error.WriteLine($"{automation.Id}: {error.Message}");
                }
            }
            return failed == 0 ? 0 : 5;
        }

        if (action == "tick")
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string state = Path.Combine(
                Directory.GetCurrentDirectory(),
                ".flunet",
                "automation-schedule.json");
            for (int index = 3; index < args.Count; index++)
            {
                if (args[index] == "--at" &&
                    ++index < args.Count &&
                    DateTimeOffset.TryParse(args[index], out DateTimeOffset parsed))
                {
                    now = parsed;
                }
                else if (args[index] == "--state" && ++index < args.Count)
                {
                    state = args[index];
                }
                else
                {
                    return UsageError($"Unknown tick argument '{args[index]}'.");
                }
            }

            AutomationScheduler scheduler = new(
                context.GetService<SentenceExecutor>(),
                new DurableAutomationScheduleStore(
                    state,
                    context.GetService<IExecutionPolicy>()));
            foreach (AutomationDefinition automation in compilation.Automations)
                await scheduler.RegisterAsync(automation, now);

            IReadOnlyList<AutomationRunResult> runs = await scheduler.TickAsync(now);
            foreach (AutomationRunResult run in runs)
                Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }

        if (action == "daemon")
            return await AutomationDaemonAsync(args, context, compilation);

        if (action == "signal")
        {
            if (args.Count < 4)
                return UsageError("Usage: flunet automation signal FILE RESOURCE [EVENT]");

            AutomationScheduler scheduler = new(
                context.GetService<SentenceExecutor>(),
                new InMemoryAutomationScheduleStore());
            foreach (AutomationDefinition automation in compilation.Automations)
                await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);

            IReadOnlyList<AutomationRunResult> runs = await scheduler.PublishSignalAsync(
                args[3],
                args.Count > 4 ? args[4] : null);
            foreach (AutomationRunResult run in runs)
                Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }

        if (action == "replay")
        {
            if (args.Count < 4)
                return UsageError("Usage: flunet automation replay FILE EVENTS_PATH [--event NAME]");

            string eventsPath = args[3];
            string? eventName = null;
            for (int index = 4; index < args.Count; index++)
            {
                if (args[index].Equals("--event", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                {
                    eventName = args[index];
                }
                else
                {
                    return UsageError($"Unknown replay argument '{args[index]}'.");
                }
            }

            AutomationScheduler scheduler = new(
                context.GetService<SentenceExecutor>(),
                new InMemoryAutomationScheduleStore());
            foreach (AutomationDefinition automation in compilation.Automations)
                await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);

            IAutomationSignalStore signalStore = CreateSignalStore(
                eventsPath,
                context.GetService<IExecutionPolicy>());
            IReadOnlyList<AutomationRunResult> runs = await scheduler.ReplaySignalsAsync(signalStore, eventName);
            foreach (AutomationRunResult run in runs)
            {
                Console.WriteLine(
                    $"{run.Signal?.EventName}: {run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            }
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }

        if (action == "watch")
        {
            if (args.Count < 5)
            {
                return UsageError(
                    "Usage: flunet automation watch FILE DIRECTORY RESOURCE [--filter PATTERN] [--recursive] [--events PATH]");
            }

            string directory = args[3];
            string resource = args[4];
            string filter = "*";
            bool recursive = false;
            string? eventsPath = null;
            for (int index = 5; index < args.Count; index++)
            {
                if (args[index].Equals("--recursive", StringComparison.OrdinalIgnoreCase))
                {
                    recursive = true;
                }
                else if (args[index].Equals("--filter", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                {
                    filter = args[index];
                }
                else if (args[index].Equals("--events", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
                {
                    eventsPath = args[index];
                }
                else
                {
                    return UsageError($"Unknown watch argument '{args[index]}'.");
                }
            }

            AutomationScheduler scheduler = new(
                context.GetService<SentenceExecutor>(),
                new InMemoryAutomationScheduleStore());
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
                FileWatchAutomationBridge bridge = new(
                    context.GetService<IFluNetFileWatcher>(),
                    scheduler,
                    signalStore);
                await bridge.RunAsync(
                    directory,
                    resource,
                    filter,
                    recursive,
                    cancellation.Token,
                    (change, runs) =>
                    {
                        foreach (AutomationRunResult run in runs)
                        {
                            Console.WriteLine(
                                $"{change.Kind}: {run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
                        }
                        return ValueTask.CompletedTask;
                    });
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
            return 0;
        }

        return UsageError($"Unknown automation action '{action}'.");
    }

    private static async Task<int> AutomationDaemonAsync(
        IReadOnlyList<string> args,
        FluNETContext context,
        AutomationCompilationResult compilation)
    {
        string state = Path.Combine(
            Directory.GetCurrentDirectory(),
            ".flunet",
            "automation-schedule.json");
        TimeSpan poll = TimeSpan.FromSeconds(1);
        for (int index = 3; index < args.Count; index++)
        {
            if (args[index].Equals("--state", StringComparison.OrdinalIgnoreCase) && ++index < args.Count)
            {
                state = args[index];
            }
            else if (args[index].Equals("--interval", StringComparison.OrdinalIgnoreCase) &&
                     ++index < args.Count &&
                     TryAutomationDuration(args[index], out TimeSpan parsed))
            {
                poll = parsed;
            }
            else
            {
                return UsageError("Usage: flunet automation daemon FILE [--state PATH] [--interval 1s]");
            }
        }

        AutomationScheduler scheduler = new(
            context.GetService<SentenceExecutor>(),
            new DurableAutomationScheduleStore(
                state,
                context.GetService<IExecutionPolicy>()));
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
            Console.WriteLine(
                $"Running {compilation.Automations.Count} automation(s). Press Ctrl+C to stop.");
            while (!cancellation.IsCancellationRequested)
            {
                IReadOnlyList<AutomationRunResult> runs = await scheduler.TickAsync(
                    DateTimeOffset.UtcNow,
                    cancellation.Token);
                foreach (AutomationRunResult run in runs)
                {
                    Console.WriteLine(
                        $"{DateTimeOffset.UtcNow:O} {run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
                }
                await Task.Delay(poll, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
        return 0;
    }

    private static bool TryAutomationDuration(string value, out TimeSpan duration)
    {
        string text = value.Trim().ToLowerInvariant();
        double multiplier = text.EndsWith("ms", StringComparison.Ordinal)
            ? 0.001
            : text.EndsWith('s')
                ? 1
                : text.EndsWith('m')
                    ? 60
                    : text.EndsWith('h')
                        ? 3600
                        : 0;
        string number = multiplier == 0
            ? string.Empty
            : text[..^(text.EndsWith("ms", StringComparison.Ordinal) ? 2 : 1)];
        if (!double.TryParse(
                number,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double parsed) ||
            parsed <= 0 ||
            parsed > 3600)
        {
            duration = default;
            return false;
        }

        duration = TimeSpan.FromSeconds(parsed * multiplier);
        return duration >= TimeSpan.FromMilliseconds(50);
    }

    private static IAutomationSignalStore CreateSignalStore(string path, IExecutionPolicy policy) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteAutomationSignalStore(path, policy)
            : new JsonFileAutomationSignalStore(path, policy);
}
