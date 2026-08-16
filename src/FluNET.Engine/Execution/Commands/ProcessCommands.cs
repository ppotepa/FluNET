using System.Text.Json;
using System.Text;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record RunProcessCommand(
    IExpression<string> FileName,
    IExpression<string> Arguments,
    IExpression<string>? WorkingDirectory = null,
    IExpression<string>? Environment = null) : ICommand<JsonElement>;

public sealed class RunProcessCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<RunProcessCommand, JsonElement>
{
    public RunProcessCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("system.process.run")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(
            context.RequireText(SemanticRole.Source),
            context.Optional<string>(SemanticRole.Theme) is { } args
                ? args
                : new LiteralExpression<string>(string.Empty),
            context.Optional<string>(new FrameRoleId("WorkingDirectory")),
            context.Optional<string>(new FrameRoleId("Environment")));
    }
}

public sealed class RunProcessCommandHandler(
    IFluNetProcessRunner runner,
    IVariableResolver variables) : ICommandHandler<RunProcessCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(
        RunProcessCommand command,
        CancellationToken cancellationToken = default)
    {
        string fileName = command.FileName.Evaluate(variables);
        string arguments = command.Arguments.Evaluate(variables);
        string? workingDirectory = command.WorkingDirectory?.Evaluate(variables);
        IReadOnlyDictionary<string, string>? environment = command.Environment is null
            ? null
            : ProcessEnvironmentParser.Parse(command.Environment.Evaluate(variables));
        FluNetProcessResult result = await runner.RunAsync(
            new FluNetProcessRequest(
                fileName,
                ProcessArgumentParser.Parse(arguments),
                WorkingDirectory: workingDirectory,
                Environment: environment),
            cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(result);
    }
}

internal static class ProcessEnvironmentParser
{
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string normalized = text.Trim();
        if (normalized is ['{', .., '}']) normalized = normalized[1..^1];

        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string assignment in SplitAssignments(normalized))
        {
            int equals = assignment.IndexOf('=');
            if (equals <= 0)
                throw new FormatException($"Environment entry '{assignment}' must use NAME=value.");
            string name = assignment[..equals].Trim();
            string value = assignment[(equals + 1)..].Trim().Trim('"', '\'');
            if (name.Length == 0 || name.Contains('=') || name.Contains('\0'))
                throw new FormatException($"Invalid environment variable name '{name}'.");
            result[name] = value;
        }
        return result;
    }

    private static IEnumerable<string> SplitAssignments(string text)
    {
        System.Text.StringBuilder current = new();
        char quote = '\0';
        foreach (char character in text)
        {
            if (character is '"' or '\'' )
            {
                quote = quote == '\0' ? character : quote == character ? '\0' : quote;
            }
            if (quote == '\0' && character is ',' or ';')
            {
                if (current.Length > 0) yield return current.ToString().Trim();
                current.Clear();
                continue;
            }
            current.Append(character);
        }
        if (quote != '\0') throw new FormatException("Process environment contains an unterminated quote.");
        if (current.Length > 0) yield return current.ToString().Trim();
    }
}

internal static class ProcessArgumentParser
{
    public static IReadOnlyList<string> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        List<string> result = [];
        StringBuilder current = new();
        char quote = '\0';
        bool escaping = false;
        foreach (char character in text)
        {
            if (escaping) { current.Append(character); escaping = false; continue; }
            if (character == '\\' && quote != '\'') { escaping = true; continue; }
            if ((character == '"' || character == '\'') && (quote == '\0' || quote == character))
            {
                quote = quote == '\0' ? character : '\0';
                continue;
            }
            if (char.IsWhiteSpace(character) && quote == '\0')
            {
                if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(character);
        }
        if (escaping) current.Append('\\');
        if (quote != '\0') throw new FormatException("Process arguments contain an unterminated quote.");
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }
}
