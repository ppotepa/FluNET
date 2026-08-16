using FluNET.Compilation;
using FluNET.Diagnostics;
using FluNET.Execution;
using FluNET.Execution.Capabilities;
using FluNET.Language;

return await FluCli.RunAsync(args);

internal static class FluCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var registry = new LanguageRegistry();
        LanguageBuildResult language = registry.Build();

        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        return command switch
        {
            "verbs" => ShowVerbs(language.Snapshot),
            "verb" => ShowVerb(language.Snapshot, args),
            "modules" => ShowModules(language.Snapshot),
            "language" => ShowLanguage(language),
            "check" => Check(language.Snapshot, args),
            "explain" => Explain(language.Snapshot, args),
            "run" => await RunScriptAsync(language.Snapshot, args),
            _ => Unknown(command)
        };
    }

    private static int ShowVerbs(LanguageSnapshot language)
    {
        foreach (IGrouping<string, VerbDescriptor> group in language.Verbs
                     .OrderBy(x => x.Text)
                     .GroupBy(x => x.Text, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"{group.Key,-16} {group.Count()} implementation(s)");
        return 0;
    }

    private static int ShowVerb(LanguageSnapshot language, string[] args)
    {
        if (args.Length < 2) return Missing("verb keyword");
        Console.WriteLine(LanguageIntrospection.ExplainVerb(language, args[1]));
        return language.GetVerbOverloads(args[1]).Count > 0 ? 0 : 2;
    }

    private static int ShowModules(LanguageSnapshot language)
    {
        if (language.Modules.Count == 0)
        {
            Console.WriteLine("No explicit language modules registered.");
            return 0;
        }

        foreach (ModuleDescriptor module in language.Modules.OrderBy(x => x.ModuleName))
            Console.WriteLine($"{module.ModuleName} {module.Version}");
        return 0;
    }

    private static int ShowLanguage(LanguageBuildResult language)
    {
        PrintDiagnostics(language.Diagnostics);
        Console.WriteLine(LanguageIntrospection.ToJson(language.Snapshot));
        return language.Success ? 0 : 2;
    }

    private static int Check(LanguageSnapshot language, string[] args)
    {
        if (!TryReadScript(args, out string? source, out int error)) return error;
        ClassicCompilation compilation = new ClassicCompiler(language).Compile(source!);
        PrintDiagnostics(compilation.Diagnostics);
        if (compilation.Success)
            Console.WriteLine($"OK: {compilation.Pipelines.Count} pipeline(s).");
        return compilation.Success ? 0 : 2;
    }

    private static int Explain(LanguageSnapshot language, string[] args)
    {
        if (!TryReadScript(args, out string? source, out int error)) return error;
        ClassicCompilation compilation = new ClassicCompiler(language).Compile(source!);
        PrintDiagnostics(compilation.Diagnostics);
        if (!compilation.Success) return 2;

        for (int p = 0; p < compilation.Pipelines.Count; p++)
        {
            Console.WriteLine($"Pipeline {p + 1}:");
            foreach (var sentence in compilation.Pipelines[p].Sentences)
            {
                Console.WriteLine($"  {sentence.Verb.Text} -> {sentence.Verb.VerbType.FullName}");
                Console.WriteLine($"    result: {sentence.ResultType?.FullName ?? "void/unknown"}");
                Console.WriteLine($"    cost: {sentence.BindingCost}");
                foreach (var role in sentence.Roles)
                    Console.WriteLine($"    {role.Descriptor.Kind}: {role.Descriptor.ValueType.Name} ({role.Descriptor.Direction}, {role.Descriptor.Cardinality})");
            }
        }
        return 0;
    }

    private static async Task<int> RunScriptAsync(LanguageSnapshot language, string[] args)
    {
        if (!TryReadScript(args, out string? source, out int error)) return error;

        string[] allowed = ReadAllowedCapabilities(args);
        ICapabilityPolicy capabilities = allowed.Length == 0
            ? AllowAllCapabilityPolicy.Instance
            : new ExplicitCapabilityPolicy(allowed);

        var engine = new ClassicScriptEngine(language, capabilities: capabilities);
        try
        {
            ClassicScriptResult result = await engine.RunAsync(source!);
            PrintDiagnostics(result.Compilation.Diagnostics);
            if (!result.Success) return 2;
            if (result.Result != null) Console.WriteLine(result.Result);
            return 0;
        }
        catch (CapabilityDeniedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Runtime error: {ex.Message}");
            return 4;
        }
    }

    private static string[] ReadAllowedCapabilities(string[] args)
    {
        int index = Array.FindIndex(args, x => x.Equals("--allow", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return [];
        return args[index + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryReadScript(string[] args, out string? source, out int error)
    {
        source = null;
        error = 0;
        if (args.Length < 2)
        {
            error = Missing("script file");
            return false;
        }

        string file = args[1];
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"Script not found: {file}");
            error = 2;
            return false;
        }

        source = File.ReadAllText(file);
        return true;
    }

    private static void PrintDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (Diagnostic diagnostic in diagnostics)
        {
            string location = diagnostic.Span is null ? string.Empty : $" [{diagnostic.Span.Start}..{diagnostic.Span.End})";
            Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.Severity}: {diagnostic.Message}{location}");
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 2;
    }

    private static int Missing(string value)
    {
        Console.Error.WriteLine($"Missing {value}.");
        return 2;
    }

    private static bool IsHelp(string value) => value is "-h" or "--help" or "help";

    private static void PrintHelp()
    {
        Console.WriteLine("FluNET.Classic 0.1");
        Console.WriteLine();
        Console.WriteLine("  flu verbs");
        Console.WriteLine("  flu verb <keyword>");
        Console.WriteLine("  flu modules");
        Console.WriteLine("  flu language");
        Console.WriteLine("  flu check <script.flu>");
        Console.WriteLine("  flu explain <script.flu>");
        Console.WriteLine("  flu run <script.flu> [--allow capability1,capability2]");
    }
}
