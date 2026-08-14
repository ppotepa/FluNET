using System.Text.Json;
using System.Runtime.InteropServices;
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

        if (options.Prompt is null && !Console.IsInputRedirected)
        {
            return await RunInteractiveAsync(options);
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
            services.AddSingleton<IExecutionPolicy>(CreateExecutionPolicy(options, roots)));
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

    private static async Task<int> RunInteractiveAsync(CliOptions options)
    {
        string[] roots = options.Roots.Count == 0
            ? [Directory.GetCurrentDirectory()]
            : options.Roots.ToArray();

        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<IExecutionPolicy>(CreateExecutionPolicy(options, roots)));
        Engine engine = context.GetEngine();

        Console.WriteLine("FluNET interactive session");
        Console.WriteLine("Type :help for help, :begin/:end for multiline input, :quit to exit.");

        bool collectingBlock = false;
        List<string> block = [];

        while (true)
        {
            Console.Write(collectingBlock ? "......> " : "flunet> ");
            string? line = await Console.In.ReadLineAsync();
            if (line is null)
            {
                Console.WriteLine();
                return 0;
            }

            string input = line.Trim();

            if (!collectingBlock && TryGetPastedBlock(line, out string? pastedBlock))
            {
                string[] pastedLines = pastedBlock!
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    .Split('\n');

                for (int index = 1; index < pastedLines.Length; index++)
                {
                    await Console.In.ReadLineAsync();
                }

                input = pastedBlock.Trim();
            }

            if (collectingBlock)
            {
                if (input.Equals(":end", StringComparison.OrdinalIgnoreCase))
                {
                    collectingBlock = false;
                    input = string.Join(Environment.NewLine, block);
                    block.Clear();
                }
                else if (input.Equals(":cancel", StringComparison.OrdinalIgnoreCase))
                {
                    collectingBlock = false;
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
            {
                continue;
            }

            if (input.Equals(":quit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals(":exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (input.Equals(":help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Enter a FluNET prompt to execute it.");
                Console.WriteLine(":begin            Start a multiline prompt block.");
                Console.WriteLine(":end              Execute the current block.");
                Console.WriteLine(":cancel            Discard the current block.");
                Console.WriteLine("Multiline clipboard pastes are detected automatically.");
                Console.WriteLine(":analyze PROMPT  Validate without executing.");
                Console.WriteLine(":quit             Exit the session.");
                continue;
            }

            if (input.Equals(":begin", StringComparison.OrdinalIgnoreCase) ||
                input.Equals(":paste", StringComparison.OrdinalIgnoreCase))
            {
                collectingBlock = true;
                block.Clear();
                Console.WriteLine("Paste the block, then type :end.");
                continue;
            }

            if (input.StartsWith(":analyze ", StringComparison.OrdinalIgnoreCase))
            {
                PromptAnalysis analysis = engine.Analyze(
                    new ProcessedPrompt(input[10..].Trim()));
                Console.WriteLine(analysis.IsValid
                    ? $"Valid ({analysis.Prompt.Syntax.Commands.Count} command(s))."
                    : analysis.ValidationResult.FailureReason);
                continue;
            }

            ExecutionResult execution = await engine.ExecuteAsync(
                new ProcessedPrompt(input));
            if (!execution.IsSuccess)
            {
                ExecutionError error = execution.Error!;
                Console.Error.WriteLine($"{error.Code} [{error.Kind}]: {error.Message}");
                continue;
            }

            bool lastStepWritesItsOwnText = execution.Plan?.Steps.LastOrDefault()?.Command.Frame.Id ==
                new FrameId("core.say.text");
            if (execution.Result is not null && !lastStepWritesItsOwnText)
            {
                Console.WriteLine(FormatResult(execution.Result));
            }
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
              flunet [options]                 Interactive session

            Options:
              --analyze          Parse and validate without executing.
              --root PATH        Allow file access under PATH (repeatable).
                                 Defaults to the current directory.
              --host HOST        Restrict HTTP/HTTPS access to HOST (repeatable).
                                 If omitted, network access is unrestricted.
              -h, --help         Show this help.

            Examples:
              flunet -- "SAY 'Hello from FluNET'."
              flunet --analyze -- "GET [text] FROM {input.txt}"
              flunet --root ./data -- "GET [text] FROM {./data/input.txt}."
              flunet --host example.com -- "DOWNLOAD [file] FROM {https://example.com/a.txt} TO {a.txt}."
            """);
    }

    private static IExecutionPolicy CreateExecutionPolicy(
        CliOptions options,
        IReadOnlyList<string> roots) =>
        options.Hosts.Count == 0
            ? new NetworkOpenFileRestrictedPolicy(roots)
            : new RestrictedExecutionPolicy(roots, options.Hosts);

    private sealed class NetworkOpenFileRestrictedPolicy(
        IReadOnlyList<string> roots) : IExecutionPolicy
    {
        private readonly RestrictedExecutionPolicy _filePolicy =
            new(roots, Array.Empty<string>());

        public void EnsureFileAccess(string path) => _filePolicy.EnsureFileAccess(path);

        public void EnsureNetworkAccess(Uri uri)
        {
        }
    }

    private static bool TryGetPastedBlock(string firstLine, out string? block)
    {
        block = null;
        if (Console.IsInputRedirected)
        {
            return false;
        }

        string? clipboard = ReadClipboardText();
        if (string.IsNullOrWhiteSpace(clipboard) ||
            (!clipboard.Contains('\n') && !clipboard.Contains('\r')))
        {
            return false;
        }

        string firstClipboardLine = clipboard
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')[0];
        if (!firstClipboardLine.Trim().Equals(firstLine.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        block = clipboard;
        return true;
    }

    private static string? ReadClipboardText()
    {
        if (!OperatingSystem.IsWindows() || !OpenClipboard(IntPtr.Zero))
        {
            return null;
        }

        try
        {
            IntPtr handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private const uint CfUnicodeText = 13;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);

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
