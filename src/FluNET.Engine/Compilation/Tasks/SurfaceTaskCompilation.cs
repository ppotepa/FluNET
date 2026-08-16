using FluNET.Language;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Tasks;

public static class SurfaceTaskHeader
{
    public static bool TryParse(
        SurfaceCommandSyntax command,
        ICollection<SurfaceDiagnostic> diagnostics,
        out string? name,
        out IReadOnlyList<string>? parameters,
        out string? resultType)
    {
        name = resultType = null;
        parameters = null;
        if (command.Values.Count != 1 || command.Alias is not null)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN290", "TASK requires `TASK name [parameters] RETURNS Type`.", command.Span));
            return false;
        }
        string[] tokens = SplitWords(command.Values[0].UnquotedText).ToArray();
        int returns = Array.FindIndex(tokens, token => token.Equals("RETURNS", StringComparison.OrdinalIgnoreCase));
        if (tokens.Any(token => token == "->"))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN290", "TASK result declarations use `RETURNS Type`.", command.Span));
            return false;
        }
        string[] declaration = returns < 0 ? tokens : tokens[..returns];
        if (returns >= 0)
        {
            if (returns + 2 != tokens.Length)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN290", "TASK result declaration must be `RETURNS Type`.", command.Span));
                return false;
            }
            resultType = tokens[returns + 1];
        }
        if (declaration.Length == 0 || declaration.Any(token => !IsIdentifier(token)))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN290", "TASK name and parameters must be identifiers.", command.Span));
            return false;
        }
        if (declaration.Skip(1).Distinct(StringComparer.OrdinalIgnoreCase).Count() != declaration.Length - 1)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN291", "TASK parameter names must be unique.", command.Span));
            return false;
        }
        name = declaration[0];
        parameters = declaration.Skip(1).ToArray();
        return true;
    }

    internal static IReadOnlyList<string> SplitWords(string source)
    {
        List<string> result = [];
        int start = -1, depth = 0; char? quote = null; bool escaped = false;
        for (int i = 0; i <= source.Length; i++)
        {
            bool end = i == source.Length; char ch = end ? ' ' : source[i];
            if (!end)
            {
                if (escaped) { escaped = false; continue; }
                if (quote is not null) { if (ch == '\\') escaped = true; else if (ch == quote) quote = null; continue; }
                if (ch is '"' or '\'') { quote = ch; if (start < 0) start = i; continue; }
                if (ch is '(' or '[' or '{') { depth++; if (start < 0) start = i; continue; }
                if (ch is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; }
            }
            if (!end && !(char.IsWhiteSpace(ch) && depth == 0)) { if (start < 0) start = i; continue; }
            if (start >= 0) { result.Add(source[start..i]); start = -1; }
        }
        return result;
    }

    private static bool IsIdentifier(string value) => value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
}

public sealed record SurfaceTaskCompilationResult(SurfaceParseResult Parse, IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

/// <summary>Expands TASK/RUN templates before policy compilation and lowering.</summary>
public sealed class SurfaceTaskCompiler(LanguageSnapshot language)
{
    private int _callIndex;

    public SurfaceTaskCompilationResult Compile(SurfaceParseResult parse)
    {
        Dictionary<string, SurfaceTaskDefinitionSyntax> tasks = new(StringComparer.OrdinalIgnoreCase);
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        Collect(parse.Program.Statements, tasks, diagnostics);
        List<SurfaceStatementSyntax> executable = Rewrite(parse.Program.Statements, tasks, [], diagnostics);
        SourceSpan span = executable.Count == 0 ? default : SourceSpan.FromBounds(executable[0].Span.Start, executable[^1].Span.End);
        SurfaceParseResult transformed = new(parse.Document, new SurfaceProgramSyntax(executable, span), diagnostics);
        return new SurfaceTaskCompilationResult(transformed, diagnostics);
    }

    private void Collect(IEnumerable<SurfaceStatementSyntax> statements, IDictionary<string, SurfaceTaskDefinitionSyntax> tasks, ICollection<SurfaceDiagnostic> diagnostics)
    {
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceTaskDefinitionSyntax task)
            {
                if (tasks.ContainsKey(task.Name))
                    diagnostics.Add(new SurfaceDiagnostic("FLN292", $"TASK '{task.Name}' is declared more than once.", task.Span));
                else if (task.ResultTypeName is string typeName && language.Types.Find(typeName) is null)
                    diagnostics.Add(new SurfaceDiagnostic("FLN293", $"TASK '{task.Name}' declares unknown result type '{typeName}'.", task.Span));
                else if (task.ResultTypeName is string resultType &&
                         !resultType.Equals("Unit", StringComparison.OrdinalIgnoreCase) &&
                         !ContainsReturn(task.Statements))
                    diagnostics.Add(new SurfaceDiagnostic("FLN299", $"TASK '{task.Name}' must contain RETURN because it declares RETURNS {resultType}.", task.Span));
                else tasks[task.Name] = task;
            }
            else if (statement is SurfaceContextSyntax context) Collect(context.Statements, tasks, diagnostics);
            else if (statement is SurfacePolicyContextSyntax policy) Collect(policy.Statements, tasks, diagnostics);
        }
    }

    private List<SurfaceStatementSyntax> Rewrite(
        IEnumerable<SurfaceStatementSyntax> statements,
        IReadOnlyDictionary<string, SurfaceTaskDefinitionSyntax> tasks,
        IReadOnlyList<string> stack,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> output = [];
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceTaskDefinitionSyntax) continue;
            if (statement is SurfaceContextSyntax context)
            {
                output.Add(context with { Statements = Rewrite(context.Statements, tasks, stack, diagnostics) });
                continue;
            }
            if (statement is SurfacePolicyContextSyntax policy)
            {
                output.Add(policy with { Statements = Rewrite(policy.Statements, tasks, stack, diagnostics) });
                continue;
            }
            if (statement is SurfaceCommandSyntax run && run.NormalizedName == "RUN")
            {
                output.AddRange(ExpandRun(run, tasks, stack, diagnostics));
                continue;
            }
            output.Add(statement);
        }
        return output;
    }

    private IReadOnlyList<SurfaceStatementSyntax> ExpandRun(
        SurfaceCommandSyntax run,
        IReadOnlyDictionary<string, SurfaceTaskDefinitionSyntax> tasks,
        IReadOnlyList<string> stack,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (run.Values.Count != 1)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN294", "RUN requires `RUN task [args] [AS result]`.", run.Span));
            return [];
        }
        string[] words = SurfaceTaskHeader.SplitWords(run.Values[0].UnquotedText).ToArray();
        if (words.Length == 0 || !tasks.TryGetValue(words[0], out SurfaceTaskDefinitionSyntax? task))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN295", $"Unknown TASK '{(words.Length == 0 ? string.Empty : words[0])}'.", run.Span));
            return [];
        }
        if (stack.Contains(task.Name, StringComparer.OrdinalIgnoreCase) || stack.Count >= 32)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN296", $"TASK expansion cycle/depth limit reached at '{task.Name}'.", run.Span));
            return [];
        }
        string[] args = words.Skip(1).ToArray();
        if (args.Length != task.Parameters.Count)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN297", $"TASK '{task.Name}' expects {task.Parameters.Count} argument(s); received {args.Length}.", run.Span));
            return [];
        }

        Dictionary<string, string> parameters = task.Parameters
            .Select((name, index) => (name, args[index]))
            .ToDictionary(item => item.name, item => item.Item2, StringComparer.OrdinalIgnoreCase);
        string prefix = $"__task_{_callIndex++:D4}_";
        Dictionary<string, string> aliases = CollectAliases(task.Statements, prefix);
        List<SurfaceStatementSyntax> cloned = task.Statements.Select(statement => Substitute(statement, parameters, aliases)).ToList();
        NormalizeReturns(cloned, run.Alias, task.ResultTypeName, diagnostics);
        if (run.Alias is not null) AssignFinalResult(cloned, run.Alias);
        return Rewrite(cloned, tasks, [.. stack, task.Name], diagnostics);
    }

    private static bool ContainsReturn(IEnumerable<SurfaceStatementSyntax> statements) => statements.Any(statement =>
        statement is SurfaceCommandSyntax command && command.NormalizedName == "RETURN" ||
        statement is SurfaceContextSyntax context && ContainsReturn(context.Statements) ||
        statement is SurfacePolicyContextSyntax policy && ContainsReturn(policy.Statements));

    private void NormalizeReturns(
        List<SurfaceStatementSyntax> statements,
        string? publicAlias,
        string? resultType,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        for (int index = statements.Count - 1; index >= 0; index--)
        {
            if (statements[index] is not SurfaceCommandSyntax command || command.NormalizedName != "RETURN")
                continue;

            if (command.Values.Count != 1 || command.Alias is not null)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN298", "RETURN requires exactly one value and cannot have AS.", command.Span));
                statements.RemoveAt(index);
                continue;
            }

            string value = command.Values[0].Text.Trim();
            string variable = value.Trim();
            if (variable.StartsWith('[') && variable.EndsWith(']'))
                variable = variable[1..^1].Trim();
            if (IsIdentifier(variable) && TryAliasReturnedVariable(statements, index, variable, publicAlias))
            {
                statements.RemoveAt(index);
                continue;
            }

            string target = publicAlias ?? $"__task_return_{_callIndex:D4}";
            if (resultType?.Equals("Text", StringComparison.OrdinalIgnoreCase) == true &&
                variable.Length > 0 && value.StartsWith('[') && value.EndsWith(']'))
                value = $"\"{{{variable}}}\"";
            statements[index] = new SurfaceCommandSyntax(
                "LET",
                [new SurfaceValueSyntax($"{target} = {value}", command.Values[0].Span)],
                null,
                command.Span);
        }
    }

    private static bool TryAliasReturnedVariable(
        List<SurfaceStatementSyntax> statements,
        int returnIndex,
        string variable,
        string? publicAlias)
    {
        for (int index = returnIndex - 1; index >= 0; index--)
        {
            if (statements[index] is SurfaceCommandSyntax command &&
                command.Alias is string alias &&
                alias.Equals(variable, StringComparison.OrdinalIgnoreCase))
            {
                statements[index] = command with { Alias = publicAlias ?? alias };
                return true;
            }
            if (statements[index] is SurfacePipelineSyntax pipeline)
            {
                SurfaceCommandSyntax[] stages = pipeline.Stages.ToArray();
                for (int stageIndex = stages.Length - 1; stageIndex >= 0; stageIndex--)
                {
                    if (!string.Equals(stages[stageIndex].Alias, variable, StringComparison.OrdinalIgnoreCase)) continue;
                    stages[stageIndex] = stages[stageIndex] with { Alias = publicAlias ?? stages[stageIndex].Alias };
                    statements[index] = pipeline with { Stages = stages };
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsIdentifier(string value) => value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');

    private static Dictionary<string, string> CollectAliases(IEnumerable<SurfaceStatementSyntax> statements, string prefix)
    {
        Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfaceCommandSyntax command && command.Alias is string alias) aliases.TryAdd(alias, prefix + alias);
            if (statement is SurfacePipelineSyntax pipeline)
                foreach (SurfaceCommandSyntax stage in pipeline.Stages)
                    if (stage.Alias is string stageAlias) aliases.TryAdd(stageAlias, prefix + stageAlias);
            if (statement is SurfaceContextSyntax context)
                foreach ((string key, string value) in CollectAliases(context.Statements, prefix)) aliases.TryAdd(key, value);
        }
        return aliases;
    }

    private static SurfaceStatementSyntax Substitute(
        SurfaceStatementSyntax statement,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string> aliases) => statement switch
    {
        SurfaceCommandSyntax command => SubstituteCommand(command, parameters, aliases),
        SurfacePipelineSyntax pipeline => pipeline with { Stages = pipeline.Stages.Select(stage => SubstituteCommand(stage, parameters, aliases)).ToArray() },
        SurfaceContextSyntax context => context with
        {
            BaseResource = SubstituteValue(context.BaseResource, parameters, aliases),
            Statements = context.Statements.Select(child => Substitute(child, parameters, aliases)).ToArray()
        },
        SurfacePolicyContextSyntax policy => policy with { Statements = policy.Statements.Select(child => Substitute(child, parameters, aliases)).ToArray() },
        _ => statement
    };

    private static SurfaceCommandSyntax SubstituteCommand(
        SurfaceCommandSyntax command,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string> aliases)
    {
        string? alias = command.Alias is string original && aliases.TryGetValue(original, out string? mapped) ? mapped : command.Alias;
        return command with
        {
            Alias = alias,
            Values = command.Values.Select(value => SubstituteValue(value, parameters, aliases)).ToArray()
        };
    }

    private static SurfaceValueSyntax SubstituteValue(
        SurfaceValueSyntax value,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string> aliases)
    {
        string text = value.Text;
        foreach ((string name, string replacement) in parameters)
            text = text.Replace($"{{{name}}}", replacement, StringComparison.OrdinalIgnoreCase);
        foreach ((string name, string replacement) in aliases)
        {
            text = text.Replace($"[{name}]", $"[{replacement}]", StringComparison.OrdinalIgnoreCase);
            if (text.Equals(name, StringComparison.OrdinalIgnoreCase)) text = replacement;
            else if (text.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase)) text = replacement + text[name.Length..];
        }
        return value with { Text = text };
    }

    private static void AssignFinalResult(List<SurfaceStatementSyntax> statements, string alias)
    {
        for (int i = statements.Count - 1; i >= 0; i--)
        {
            if (statements[i] is SurfaceCommandSyntax command && ProducesValue(command))
            {
                statements[i] = command with { Alias = alias };
                return;
            }
            if (statements[i] is SurfacePipelineSyntax pipeline)
            {
                SurfaceCommandSyntax[] stages = pipeline.Stages.ToArray();
                for (int j = stages.Length - 1; j >= 0; j--)
                {
                    if (!ProducesValue(stages[j])) continue;
                    stages[j] = stages[j] with { Alias = alias };
                    statements[i] = pipeline with { Stages = stages };
                    return;
                }
            }
        }
    }

    private static bool ProducesValue(SurfaceCommandSyntax command) =>
        command.Alias is not null ||
        command.NormalizedName is "GET" or "LOAD" or "FILTER" or "WHERE" or "SORT" or "ORDER" or
        "TAKE" or "SKIP" or "DISTINCT" or "SELECT" or "MAP" or "DEFAULT" or "GROUP" or "SUM" or
        "COUNT" or "AVG" or "MIN" or "MAX" or "JOIN" or "MATCH" or "LET";
}
