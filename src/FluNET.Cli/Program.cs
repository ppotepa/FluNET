using System.Text.Json;
<<<<<<< HEAD
using System.Runtime.InteropServices;
using FluNET;
using FluNET.Automation;
using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Declarative;
using FluNET.Execution;
using FluNET.Execution.Planning;
using FluNET.Language;
using FluNET.Prompt;
using FluNET.Tooling;
=======
using FluNET;
using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using FluNET.Syntax.Verbs;
>>>>>>> origin/agent/stabilize-poc-foundation
using Microsoft.Extensions.DependencyInjection;

return await FluNetCli.RunAsync(args);

internal static class FluNetCli
{
    public static async Task<int> RunAsync(string[] args)
    {
<<<<<<< HEAD
        if (args.Length > 0 && args[0].Equals("automation", StringComparison.OrdinalIgnoreCase))
            return await RunAutomationToolAsync(args);
        if (args.Length > 0 && args[0].Equals("ensure", StringComparison.OrdinalIgnoreCase))
            return await RunEnsureToolAsync(args);
        if (args.Length > 0 && IsSurfaceTool(args[0]))
            return await RunSurfaceToolAsync(args);

        CliOptions options;
        try { options = CliOptions.Parse(args); }
=======
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
>>>>>>> origin/agent/stabilize-poc-foundation
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Run 'flunet --help' for usage.");
            return 64;
        }
<<<<<<< HEAD
        if (options.Help) { PrintHelp(); return 0; }
        if (options.Prompt is null && !Console.IsInputRedirected) return await RunInteractiveAsync(options);
        string promptText = options.Prompt ?? await Console.In.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(promptText)) { Console.Error.WriteLine("A prompt is required."); return 64; }
        string[] roots = options.Roots.Count == 0 ? [Directory.GetCurrentDirectory()] : options.Roots.ToArray();
        using FluNETContext context = FluNETContext.Create(services => services.AddSingleton<IExecutionPolicy>(CreateExecutionPolicy(options, roots)));
        Engine engine = context.GetEngine();
        ProcessedPrompt prompt = new(promptText.Trim());
        if (options.Analyze)
        {
            PromptAnalysis analysis = engine.Analyze(prompt);
            if (analysis.IsValid) { Console.WriteLine($"Valid ({analysis.Prompt.Syntax.Commands.Count} command(s))."); return 0; }
            Console.Error.WriteLine(analysis.ValidationResult.FailureReason); return prompt.IsValid ? 3 : 2;
        }
        return await ExecuteCanonicalAsync(engine, prompt);
    }

    private static bool IsSurfaceTool(string command) =>
        command.Equals("check", StringComparison.OrdinalIgnoreCase) || command.Equals("fmt", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("explain", StringComparison.OrdinalIgnoreCase) || command.Equals("graph", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("run", StringComparison.OrdinalIgnoreCase);

    private static async Task<int> RunSurfaceToolAsync(IReadOnlyList<string> args)
    {
        string command = args[0].ToLowerInvariant();
        string source;
        try { source = await ReadSourceAsync(args.Count > 1 ? args[1] : null); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { Console.Error.WriteLine(exception.Message); return 66; }

        if (command == "fmt")
        {
            try { Console.WriteLine(new SurfaceFormatter().Format(source)); return 0; }
            catch (FormatException exception) { Console.Error.WriteLine(exception.Message); return 2; }
        }

        using FluNETContext context = CreateCliSurfaceContext();
        SurfaceCompiler compiler = context.GetSurfaceCompiler();
        if (command == "check")
        {
            SurfaceCompilationResult result = new SurfaceCheckService(compiler).Check(source);
            if (result.IsValid) { Console.WriteLine($"Valid ({result.Plan!.Steps.Count} step(s), {result.Lowering.InferenceTrace.Items.Count} inference decision(s))."); return 0; }
            PrintSurfaceDiagnostics(result); return 3;
        }
        if (command == "explain")
        {
            SurfaceExplanation explanation = new SurfaceExplainService(compiler).Explain(source);
            Console.WriteLine(explanation.Text); return explanation.Compilation.IsValid ? 0 : 3;
        }
        if (command == "graph")
        {
            SurfaceCompilationResult result = compiler.Compile(new FluNET.Prompt.Surface.SourceDocument(source));
            if (!result.IsValid) { PrintSurfaceDiagnostics(result); return 3; }
            Console.WriteLine(new SurfaceGraphExporter().ToDot(result)); return 0;
        }
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(source);
        if (!execution.Compilation.IsValid) { PrintSurfaceDiagnostics(execution.Compilation); return 3; }
        if (execution.Error is not null) { Console.Error.WriteLine(execution.Error.Message); return execution.Error is OperationCanceledException ? 130 : 5; }
        PrintResultUnlessSay(execution.Result, execution.Compilation.Plan); return 0;
    }

    private static async Task<int> RunAutomationToolAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3)
        {
            Console.Error.WriteLine("Usage: flunet automation check|run|tick|signal FILE [arguments]");
            return 64;
        }
        string action = args[1].ToLowerInvariant();
        string source;
        try { source = await ReadSourceAsync(args[2]); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { Console.Error.WriteLine(exception.Message); return 66; }

        using FluNETContext context = CreateCliSurfaceContext();
        AutomationCompilationResult compilation = context.CompileAutomations(source);
        if (!compilation.IsValid)
        {
            foreach (AutomationDiagnostic diagnostic in compilation.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            return 3;
        }
        if (action == "check")
        {
            Console.WriteLine($"Valid ({compilation.Automations.Count} automation(s)).");
            foreach (AutomationDefinition automation in compilation.Automations)
                Console.WriteLine($"{automation.Id}: {automation.Trigger.GetType().Name}");
            return 0;
        }
        if (action == "run")
        {
            int failures = 0;
            foreach (AutomationDefinition automation in compilation.Automations)
            {
                List<ExecutionStepResult> steps = [];
                try
                {
                    object? result = await context.GetService<ExecutionPlanExecutor>().ExecuteAsync(automation.Template.Compilation.Plan!, steps);
                    Console.WriteLine($"{automation.Id}: succeeded ({steps.Count} step(s))");
                    if (result is not null) Console.WriteLine(FormatResult(result));
                }
                catch (Exception exception) { failures++; Console.Error.WriteLine($"{automation.Id}: {exception.Message}"); }
            }
            return failures == 0 ? 0 : 5;
        }
        if (action == "tick")
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string statePath = Path.Combine(Directory.GetCurrentDirectory(), ".flunet", "automation-schedule.json");
            for (int index = 3; index < args.Count; index++)
            {
                if (args[index] == "--at" && ++index < args.Count && DateTimeOffset.TryParse(args[index], out DateTimeOffset parsed)) now = parsed;
                else if (args[index] == "--state" && ++index < args.Count) statePath = args[index];
                else { Console.Error.WriteLine($"Unknown automation tick argument '{args[index]}'."); return 64; }
            }
            AutomationScheduler scheduler = new(context.GetService<ExecutionPlanExecutor>(), new DurableAutomationScheduleStore(statePath, context.GetService<IExecutionPolicy>()));
            foreach (AutomationDefinition automation in compilation.Automations) await scheduler.RegisterAsync(automation, now);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.TickAsync(now);
            foreach (AutomationRunResult run in runs) Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        if (action == "signal")
        {
            if (args.Count < 4) { Console.Error.WriteLine("Usage: flunet automation signal FILE RESOURCE [EVENT]"); return 64; }
            AutomationScheduler scheduler = new(context.GetService<ExecutionPlanExecutor>(), new InMemoryAutomationScheduleStore());
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (AutomationDefinition automation in compilation.Automations) await scheduler.RegisterAsync(automation, now);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.PublishSignalAsync(args[3], args.Count > 4 ? args[4] : null);
            foreach (AutomationRunResult run in runs) Console.WriteLine($"{run.Automation.Id}: {(run.IsSuccess ? "succeeded" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        Console.Error.WriteLine($"Unknown automation command '{action}'."); return 64;
    }

    private static async Task<int> RunEnsureToolAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 3) { Console.Error.WriteLine("Usage: flunet ensure check|apply FILE"); return 64; }
        string action = args[1].ToLowerInvariant();
        string source;
        try { source = await ReadSourceAsync(args[2]); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { Console.Error.WriteLine(exception.Message); return 66; }
        using FluNETContext context = CreateCliSurfaceContext();
        DesiredStateCompilationResult compilation = context.CompileEnsure(source);
        if (!compilation.IsValid)
        {
            foreach (DesiredStateDiagnostic diagnostic in compilation.Diagnostics) Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            foreach (DesiredStatePlan plan in compilation.Plans.Where(plan => !plan.IsValid)) PrintSurfaceDiagnostics(plan.Compilation);
            return 3;
        }
        if (action == "check") { Console.WriteLine($"Valid ({compilation.Plans.Count} ENSURE goal(s))."); return 0; }
        if (action == "apply")
        {
            IReadOnlyList<EnsureRunResult> runs = await context.ExecuteEnsureAsync(source);
            foreach (EnsureRunResult run in runs) Console.WriteLine($"{run.Plan.Goal.Target}: {(run.IsSuccess ? "satisfied" : "failed")}");
            return runs.Any(run => !run.IsSuccess) ? 5 : 0;
        }
        Console.Error.WriteLine($"Unknown ensure command '{action}'."); return 64;
    }

    private static FluNETContext CreateCliSurfaceContext()
    {
        string[] roots = [Directory.GetCurrentDirectory()];
        return SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IExecutionPolicy>(new NetworkOpenFileRestrictedPolicy(roots)));
    }

    private static async Task<string> ReadSourceAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "-")
        {
            if (!Console.IsInputRedirected) throw new ArgumentException("A source file or redirected stdin is required.");
            return await Console.In.ReadToEndAsync();
        }
        return await File.ReadAllTextAsync(path);
    }

    private static void PrintSurfaceDiagnostics(SurfaceCompilationResult result)
    {
        foreach (var diagnostic in result.Lowering.Diagnostics) Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        foreach (CompilationDiagnostic diagnostic in result.Diagnostics) Console.Error.WriteLine($"{diagnostic.Code} [{diagnostic.Phase}]: {diagnostic.Message}");
    }

    private static async Task<int> ExecuteCanonicalAsync(Engine engine, ProcessedPrompt prompt)
    {
        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler handler = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
=======

        if (options.Help)
        {
            PrintHelp();
            return 0;
        }

        string promptText = options.Prompt ?? (Console.IsInputRedirected
            ? await Console.In.ReadToEndAsync()
            : string.Empty);
        if (string.IsNullOrWhiteSpace(promptText))
        {
            Console.Error.WriteLine("A prompt is required.");
            Console.Error.WriteLine("Run 'flunet --help' for usage.");
            return 64;
        }

        string[] roots = options.Roots.Count == 0
            ? [Directory.GetCurrentDirectory()]
            : options.Roots.ToArray();

        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<IExecutionPolicy>(new RestrictedExecutionPolicy(
                roots,
                options.Hosts)));
        Engine engine = context.GetEngine();
        ProcessedPrompt prompt = new(promptText.Trim());

        if (options.Analyze)
        {
            PromptAnalysis analysis = engine.Analyze(prompt);
            if (analysis.IsValid)
            {
                Console.WriteLine($"Valid ({analysis.Prompt.Syntax.Commands.Count} command(s)).");
                return 0;
            }

            Console.Error.WriteLine(analysis.ValidationResult.FailureReason);
            return prompt.IsValid ? 3 : 2;
        }

        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;

>>>>>>> origin/agent/stabilize-poc-foundation
        try
        {
            ExecutionResult execution = await engine.ExecuteAsync(prompt, cancellation.Token);
            if (!execution.IsSuccess)
            {
                ExecutionError error = execution.Error!;
                Console.Error.WriteLine($"{error.Code} [{error.Kind}]: {error.Message}");
<<<<<<< HEAD
                return error.Kind switch { ExecutionFailureKind.Syntax => 2, ExecutionFailureKind.Validation => 3, ExecutionFailureKind.Capability => 4, ExecutionFailureKind.Cancelled => 130, _ => 5 };
            }
            PrintResultUnlessSay(execution.Result, execution.Plan); return 0;
        }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static void PrintResultUnlessSay(object? result, ExecutionPlan? plan)
    {
        bool say = plan?.Steps.LastOrDefault()?.Command.Frame.Id == new FrameId("core.say.text");
        if (result is not null && !say) Console.WriteLine(FormatResult(result));
    }

    private static async Task<int> RunInteractiveAsync(CliOptions options)
    {
        string[] roots = options.Roots.Count == 0 ? [Directory.GetCurrentDirectory()] : options.Roots.ToArray();
        using FluNETContext context = FluNETContext.Create(services => services.AddSingleton<IExecutionPolicy>(CreateExecutionPolicy(options, roots)));
        Engine engine = context.GetEngine();
        Console.WriteLine("FluNET interactive session");
        Console.WriteLine("Type :help for help, :begin/:end for multiline input, :quit to exit.");
        bool collecting = false; List<string> block = [];
        while (true)
        {
            Console.Write(collecting ? "......> " : "flunet> ");
            string? line = await Console.In.ReadLineAsync();
            if (line is null) { Console.WriteLine(); return 0; }
            string input = line.Trim();
            if (collecting)
            {
                if (input.Equals(":end", StringComparison.OrdinalIgnoreCase)) { collecting = false; input = string.Join(Environment.NewLine, block); block.Clear(); }
                else if (input.Equals(":cancel", StringComparison.OrdinalIgnoreCase)) { collecting = false; block.Clear(); Console.WriteLine("Block cancelled."); continue; }
                else { block.Add(line); continue; }
            }
            if (input.Length == 0) continue;
            if (input is ":quit" or ":exit" or "quit" or "exit") return 0;
            if (input.Equals(":help", StringComparison.OrdinalIgnoreCase))
            { Console.WriteLine(":begin/:end multiline | :analyze PROMPT | :quit"); continue; }
            if (input.Equals(":begin", StringComparison.OrdinalIgnoreCase) || input.Equals(":paste", StringComparison.OrdinalIgnoreCase))
            { collecting = true; block.Clear(); Console.WriteLine("Paste the block, then type :end."); continue; }
            if (input.StartsWith(":analyze ", StringComparison.OrdinalIgnoreCase))
            { PromptAnalysis analysis = engine.Analyze(new ProcessedPrompt(input[10..].Trim())); Console.WriteLine(analysis.IsValid ? $"Valid ({analysis.Prompt.Syntax.Commands.Count} command(s))." : analysis.ValidationResult.FailureReason); continue; }
            _ = await ExecuteCanonicalAsync(engine, new ProcessedPrompt(input));
=======
                return error.Kind switch
                {
                    ExecutionFailureKind.Syntax => 2,
                    ExecutionFailureKind.Validation => 3,
                    ExecutionFailureKind.Capability => 4,
                    ExecutionFailureKind.Cancelled => 130,
                    _ => 5
                };
            }

            if (execution.Result is not null && execution.Sentence?.Root is not SayText)
            {
                Console.WriteLine(FormatResult(execution.Result));
            }
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
>>>>>>> origin/agent/stabilize-poc-foundation
        }
    }

    private static string FormatResult(object result) => result switch
    {
        string text => text,
        string[] lines => string.Join(Environment.NewLine, lines),
        FileInfo file => file.FullName,
        _ => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
    };

<<<<<<< HEAD
    private static void PrintHelp() => Console.WriteLine("""
        FluNET command-line runner

        Canonical:
          flunet [options] -- "PROMPT"
          flunet [options]                         Interactive session

        Compact:
          flunet check FILE
          flunet fmt FILE
          flunet explain FILE
          flunet graph FILE
          flunet run FILE

        Automation:
          flunet automation check FILE
          flunet automation run FILE
          flunet automation tick FILE [--at TIMESTAMP] [--state PATH]
          flunet automation signal FILE RESOURCE [EVENT]

        Desired state:
          flunet ensure check FILE
          flunet ensure apply FILE

        Options:
          --analyze          Analyze canonical syntax without effects.
          --root PATH        Allowed file root (repeatable; defaults to current directory).
          --host HOST        Restrict network access to HOST (repeatable; omitted = open network).
          -h, --help         Show help.
        """);

    private static IExecutionPolicy CreateExecutionPolicy(CliOptions options, IReadOnlyList<string> roots) =>
        options.Hosts.Count == 0 ? new NetworkOpenFileRestrictedPolicy(roots) : new RestrictedExecutionPolicy(roots, options.Hosts);

    private sealed class NetworkOpenFileRestrictedPolicy(IReadOnlyList<string> roots) : IExecutionPolicy
    {
        private readonly RestrictedExecutionPolicy files = new(roots, Array.Empty<string>());
        public void EnsureFileAccess(string path) => files.EnsureFileAccess(path);
        public void EnsureNetworkAccess(Uri uri) { }
    }

    private sealed record CliOptions(bool Help, bool Analyze, IReadOnlyList<string> Roots, IReadOnlyList<string> Hosts, string? Prompt)
    {
        public static CliOptions Parse(IReadOnlyList<string> args)
        {
            bool help=false, analyze=false, promptStarted=false;List<string>roots=[],hosts=[],prompt=[];
            for(int index=0;index<args.Count;index++)
            {
                string argument=args[index];if(promptStarted){prompt.Add(argument);continue;}
                switch(argument){case"--":promptStarted=true;break;case"-h"or"--help":help=true;break;case"--analyze":analyze=true;break;case"--root":roots.Add(RequireValue(args,ref index,"--root"));break;case"--host":hosts.Add(RequireValue(args,ref index,"--host"));break;default:if(argument.StartsWith('-'))throw new ArgumentException($"Unknown option '{argument}'.");promptStarted=true;prompt.Add(argument);break;}
            }
            return new(help,analyze,roots,hosts,prompt.Count==0?null:string.Join(' ',prompt));
        }
        private static string RequireValue(IReadOnlyList<string>args,ref int index,string option){if(++index>=args.Count||string.IsNullOrWhiteSpace(args[index]))throw new ArgumentException($"Option '{option}' requires a value.");return args[index];}
=======
    private static void PrintHelp()
    {
        Console.WriteLine("""
            FluNET command-line runner

            Usage:
              flunet [options] -- "PROMPT"
              echo "PROMPT" | flunet [options]

            Options:
              --analyze          Parse and validate without executing.
              --root PATH        Allow file access under PATH (repeatable).
                                 Defaults to the current directory.
              --host HOST        Allow HTTP/HTTPS access to HOST (repeatable).
              -h, --help         Show this help.

            Examples:
              flunet -- "SAY 'Hello from FluNET'."
              flunet --analyze -- "GET [text] FROM {input.txt}"
              flunet --root ./data -- "GET [text] FROM {./data/input.txt}."
              flunet --host example.com -- "DOWNLOAD [file] FROM {https://example.com/a.txt} TO {a.txt}."
            """);
    }

    private sealed record CliOptions(
        bool Help,
        bool Analyze,
        IReadOnlyList<string> Roots,
        IReadOnlyList<string> Hosts,
        string? Prompt)
    {
        public static CliOptions Parse(IReadOnlyList<string> args)
        {
            bool help = false;
            bool analyze = false;
            List<string> roots = [];
            List<string> hosts = [];
            List<string> prompt = [];
            bool promptStarted = false;

            for (int index = 0; index < args.Count; index++)
            {
                string argument = args[index];
                if (promptStarted)
                {
                    prompt.Add(argument);
                    continue;
                }

                switch (argument)
                {
                    case "--":
                        promptStarted = true;
                        break;
                    case "-h" or "--help":
                        help = true;
                        break;
                    case "--analyze":
                        analyze = true;
                        break;
                    case "--root":
                        roots.Add(RequireValue(args, ref index, "--root"));
                        break;
                    case "--host":
                        hosts.Add(RequireValue(args, ref index, "--host"));
                        break;
                    default:
                        if (argument.StartsWith('-'))
                        {
                            throw new ArgumentException($"Unknown option '{argument}'.");
                        }
                        promptStarted = true;
                        prompt.Add(argument);
                        break;
                }
            }

            return new CliOptions(help, analyze, roots, hosts,
                prompt.Count == 0 ? null : string.Join(' ', prompt));
        }

        private static string RequireValue(
            IReadOnlyList<string> args,
            ref int index,
            string option)
        {
            if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
            {
                throw new ArgumentException($"Option '{option}' requires a value.");
            }
            return args[index];
        }
>>>>>>> origin/agent/stabilize-poc-foundation
    }
}
