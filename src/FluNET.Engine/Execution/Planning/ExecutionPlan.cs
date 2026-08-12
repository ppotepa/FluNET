using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Variables;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace FluNET.Execution.Planning;

public sealed record ResultBinding
{
    private readonly ReadOnlyCollection<string> _targets;

    internal ResultBinding(string reference, IEnumerable<string> targets, bool isDestructuring)
    {
        Reference = reference;
        _targets = Array.AsReadOnly(targets.ToArray());
        IsDestructuring = isDestructuring;
    }

    public string Reference { get; }
    public IReadOnlyList<string> Targets => _targets;
    public bool IsDestructuring { get; }
}

public enum ExecutionDependencyKind
{
    Sequence,
    Variable
}

public sealed record ExecutionDependency(
    int PredecessorIndex,
    ExecutionDependencyKind Kind,
    string? Variable = null);

public sealed class ExecutionPlanStep
{
    private readonly ReadOnlyCollection<ExecutionDependency> _dependencies;

    internal ExecutionPlanStep(
        int index,
        BoundCommand command,
        ResultBinding? resultBinding,
        IEnumerable<ExecutionDependency> dependencies)
    {
        Index = index;
        Command = command;
        ResultBinding = resultBinding;
        _dependencies = Array.AsReadOnly(dependencies.ToArray());
    }

    public int Index { get; }
    public BoundCommand Command { get; }
    public ResultBinding? ResultBinding { get; }
    public IReadOnlyList<ExecutionDependency> Dependencies => _dependencies;
}

/// <summary>An immutable, currently sequential orchestration graph.</summary>
public sealed class ExecutionPlan
{
    private readonly ReadOnlyCollection<ExecutionPlanStep> _steps;

    internal ExecutionPlan(IEnumerable<ExecutionPlanStep> steps)
    {
        _steps = Array.AsReadOnly(steps.ToArray());
    }

    public IReadOnlyList<ExecutionPlanStep> Steps => _steps;
}

public sealed class ExecutionPlanner
{
    public ExecutionPlan Create(IReadOnlyList<BoundCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        List<ExecutionPlanStep> steps = [];
        Dictionary<string, int> producers = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < commands.Count; index++)
        {
            BoundCommand command = commands[index];
            ResultBinding? resultBinding = FindResultBinding(command);
            List<ExecutionDependency> dependencies = index == 0
                ? []
                : [new ExecutionDependency(index - 1, ExecutionDependencyKind.Sequence)];

            foreach (string variable in FindInputVariables(command))
            {
                if (producers.TryGetValue(variable, out int producer))
                {
                    dependencies.Add(new ExecutionDependency(
                        producer,
                        ExecutionDependencyKind.Variable,
                        variable));
                }
            }

            steps.Add(new ExecutionPlanStep(index, command, resultBinding, dependencies));
            if (resultBinding is not null)
            {
                foreach (string target in resultBinding.Targets)
                {
                    producers[target] = index;
                }
            }
        }

        return new ExecutionPlan(steps);
    }

    private static IEnumerable<string> FindInputVariables(BoundCommand command) =>
        command.Arguments.Values
            .Where(argument => argument.Slot.Direction == SlotDirection.Input)
            .SelectMany(argument => argument.Tokens)
            .Where(token => token.Kind == Prompt.PromptTokenKind.Variable)
            .Select(token => token.Text.TrimEnd('.'))
            .Select(reference => reference.Length >= 2 ? reference[1..^1] : reference)
            .Where(reference => !reference.StartsWith('{'));

    private static ResultBinding? FindResultBinding(BoundCommand command)
    {
        BoundArgument? output = command.Arguments.Values.SingleOrDefault(argument =>
            argument.Slot.Direction == SlotDirection.Output);
        if (output?.Tokens.Count != 1 || output.Tokens[0].Kind != Prompt.PromptTokenKind.Variable)
        {
            return null;
        }

        string reference = output.Tokens[0].Text.TrimEnd('.');
        string inner = reference.Length >= 2 ? reference[1..^1].Trim() : string.Empty;
        bool destructuring = inner.Length >= 2 && inner[0] == '{' && inner[^1] == '}';
        string[] targets = destructuring
            ? inner[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [inner];
        return targets.Length == 0 || targets.Any(string.IsNullOrWhiteSpace)
            ? throw new ExecutionPlanException($"Invalid output binding '{reference}'.")
            : new ResultBinding(reference, targets, destructuring);
    }
}

public sealed record ExecutionStepResult(ExecutionPlanStep Step, object? Result);

public sealed class ExecutionPlanExecutor(
    CommandDispatcher dispatcher,
    IVariableResolver variables)
{
    public async ValueTask<object?> ExecuteAsync(
        ExecutionPlan plan,
        ICollection<ExecutionStepResult> completedSteps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(completedSteps);
        object? lastResult = null;
        HashSet<int> completed = [];

        foreach (ExecutionPlanStep step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (step.Dependencies.Any(dependency => !completed.Contains(dependency.PredecessorIndex)))
            {
                throw new ExecutionPlanException(
                    $"Dependencies for execution step {step.Index} are not satisfied.");
            }
            CommandDispatchResult dispatch = await dispatcher
                .TryExecuteAsync(step.Command, cancellationToken)
                .ConfigureAwait(false);
            if (!dispatch.IsHandled)
            {
                throw new CommandRouteNotFoundException(
                    $"No typed route is registered for " +
                    $"'{step.Command.Command.Name}/{step.Command.Frame.UsageName}'.");
            }

            lastResult = dispatch.Result;
            if (lastResult is not null && step.ResultBinding is not null)
            {
                Store(step.ResultBinding, lastResult);
            }
            completedSteps.Add(new ExecutionStepResult(step, lastResult));
            completed.Add(step.Index);
        }

        return lastResult;
    }

    private void Store(ResultBinding binding, object result)
    {
        if (!binding.IsDestructuring)
        {
            variables.Register(binding.Targets[0], result);
            return;
        }

        IReadOnlyDictionary<string, object> properties = ExtractProperties(result);
        foreach (string target in binding.Targets)
        {
            if (properties.TryGetValue(target, out object? value))
            {
                variables.Register(target, value);
            }
        }
    }

    private static IReadOnlyDictionary<string, object> ExtractProperties(object result)
    {
        if (result is IReadOnlyDictionary<string, object> readOnly)
        {
            return new Dictionary<string, object>(readOnly, StringComparer.OrdinalIgnoreCase);
        }

        string? json = result switch
        {
            string value => value,
            string[] lines => string.Join('\n', lines),
            JsonElement element => element.GetRawText(),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            return document.RootElement.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ExtractJsonValue(property.Value),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static object ExtractJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.TryGetInt32(out int integer) ? integer : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Array => element.EnumerateArray().Select(ExtractJsonValue).ToArray(),
        JsonValueKind.Object => element.Deserialize<Dictionary<string, object>>() ?? [],
        _ => element.ToString()
    };
}

public sealed class ExecutionPlanException(string message) : Exception(message);

public sealed class CommandRouteNotFoundException(string message) : Exception(message);
