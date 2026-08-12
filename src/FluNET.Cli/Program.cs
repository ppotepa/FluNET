using System.Text.Json;
using FluNET;
using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Language;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

return await FluNetCli.RunAsync(args);

internal static class FluNetCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Run 'flunet --help' for usage.");
            return 64;
        }

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

        try
        {
            ExecutionResult execution = await engine.ExecuteAsync(prompt, cancellation.Token);
            if (!execution.IsSuccess)
            {
                ExecutionError error = execution.Error!;
                Console.Error.WriteLine($"{error.Code} [{error.Kind}]: {error.Message}");
                return error.Kind switch
                {
                    ExecutionFailureKind.Syntax => 2,
                    ExecutionFailureKind.Validation => 3,
                    ExecutionFailureKind.Capability => 4,
                    ExecutionFailureKind.Cancelled => 130,
                    _ => 5
                };
            }

            bool lastStepWritesItsOwnText = execution.Plan?.Steps.LastOrDefault()?.Command.Frame.Id ==
                new FrameId("core.say.text");
            if (execution.Result is not null && !lastStepWritesItsOwnText)
            {
                Console.WriteLine(FormatResult(execution.Result));
            }
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static string FormatResult(object result) => result switch
    {
        string text => text,
        string[] lines => string.Join(Environment.NewLine, lines),
        FileInfo file => file.FullName,
        _ => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
    };

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
    }
}
