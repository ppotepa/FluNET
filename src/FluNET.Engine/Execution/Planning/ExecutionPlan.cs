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
    public TypeSymbol Type { get; internal set; } = null!;
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
    private readonly ReadOnlyCollection<VariableSymbol> _variables;

    internal ExecutionPlan(
        IEnumerable<ExecutionPlanStep> steps,
        IEnumerable<VariableSymbol>? variables = null)
    {
        _steps = Array.AsReadOnly(steps.ToArray());
        _variables = Array.AsReadOnly(variables?.ToArray() ?? Array.Empty<VariableSymbol>());
    }

    public IReadOnlyList<ExecutionPlanStep> Steps => _steps;
    public IReadOnlyList<VariableSymbol> Variables => _variables;
}

public sealed class ExecutionPlanner
{
    public ExecutionPlan Create(
        IReadOnlyList<BoundCommand> commands,
        Prompt.PromptSyntax? syntax = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (syntax is not null && syntax.Commands.Count != commands.Count)
        {
            throw new ExecutionPlanException("Syntax and semantic command counts do not match.");
        }
        List<ExecutionPlanStep> steps = [];
        Dictionary<string, int> producers = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, VariableSymbol> symbols = new(StringComparer.OrdinalIgnoreCase);
        int[] stages = BuildStages(commands.Count, syntax);

        for (int index = 0; index < commands.Count; index++)
        {
            BoundCommand command = commands[index];
            ResultBinding? resultBinding = FindResultBinding(command);
            List<ExecutionDependency> dependencies = stages[index] == 0
                ? []
                : Enumerable.Range(0, index)
                    .Where(predecessor => stages[predecessor] == stages[index] - 1)
                    .Select(predecessor => new ExecutionDependency(
                        predecessor,
                        ExecutionDependencyKind.Sequence))
                    .ToList();

            foreach ((string variable, TypeSymbol expectedType) in FindInputVariables(command))
            {
                if (producers.TryGetValue(variable, out int producer))
                {
                    VariableSymbol symbol = symbols[variable];
                    if (!expectedType.IsAssignableFrom(symbol.Type))
                    {
                        throw new ExecutionPlanException(
                            $"Variable [{variable}] has type '{symbol.Type}', but " +
                            $"{command.Command.Name}/{command.Frame.UsageName} expects '{expectedType}'.");
                    }
                    dependencies.Add(new ExecutionDependency(
                        producer,
                        ExecutionDependencyKind.Variable,
                        variable));
                }
            }

            steps.Add(new ExecutionPlanStep(index, command, resultBinding, dependencies));
            if (resultBinding is not null)
            {
                resultBinding.Type = command.Frame.ResultTypeSymbol;
                foreach (string target in resultBinding.Targets)
                {
                    if (symbols.TryGetValue(target, out VariableSymbol? previous) &&
                        stages[previous.ProducerIndex] == stages[index])
                    {
                        throw new ExecutionPlanException(
                            $"Parallel steps {previous.ProducerIndex} and {index} both write [{target}].");
                    }
                    producers[target] = index;
                    symbols[target] = new VariableSymbol(target, resultBinding.Type, index);
                }
            }
        }

        return new ExecutionPlan(steps, symbols.Values.OrderBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<(string Name, TypeSymbol ExpectedType)> FindInputVariables(BoundCommand command) =>
        command.Arguments.Values
            .Where(argument => argument.Slot.Direction == SlotDirection.Input)
            .SelectMany(argument => argument.Tokens.Select(token => (argument, token)))
            .Where(pair => pair.token.Kind == Prompt.PromptTokenKind.Variable)
            .Select(pair => (
                Reference: pair.token.Text.TrimEnd('.'),
                ExpectedType: pair.argument.Slot.ValueTypeSymbol))
            .Select(pair => (
                Name: pair.Reference.Length >= 2 ? pair.Reference[1..^1] : pair.Reference,
                pair.ExpectedType))
            .Where(pair => !pair.Name.StartsWith('{'));

    private static int[] BuildStages(int commandCount, Prompt.PromptSyntax? syntax)
    {
        int[] stages = new int[commandCount];
        if (commandCount < 2)
        {
            return stages;
        }

        if (syntax is null || syntax.Links.Count == 0)
        {
            for (int index = 1; index < commandCount; index++)
            {
                stages[index] = index;
            }
            return stages;
        }

        Dictionary<int, Prompt.CommandLinkSyntax> links = syntax.Links
            .ToDictionary(link => link.SuccessorIndex);
        for (int index = 1; index < commandCount; index++)
        {
            if (!links.TryGetValue(index, out Prompt.CommandLinkSyntax? link))
            {
                throw new ExecutionPlanException($"Command {index} has no connector from its predecessor.");
            }
            stages[index] = stages[index - 1] +
                (link.Kind == Prompt.CommandLinkKind.Sequence ? 1 : 0);
        }
        return stages;
    }

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
        Dictionary<int, ExecutionPlanStep> pending = plan.Steps.ToDictionary(step => step.Index);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionPlanStep[] ready = pending.Values
                .Where(step => step.Dependencies.All(dependency =>
                    completed.Contains(dependency.PredecessorIndex)))
                .OrderBy(step => step.Index)
                .ToArray();
            if (ready.Length == 0)
            {
                throw new ExecutionPlanException(
                    "The execution graph contains an unsatisfied dependency cycle.");
            }

            StepDispatch[] dispatched = await Task.WhenAll(ready.Select(step =>
                DispatchAsync(step, cancellationToken))).ConfigureAwait(false);
            foreach (StepDispatch item in dispatched.OrderBy(item => item.Step.Index))
            {
                lastResult = item.Result;
                if (lastResult is not null && item.Step.ResultBinding is not null)
                {
                    Store(item.Step.ResultBinding, lastResult);
                }
                completedSteps.Add(new ExecutionStepResult(item.Step, lastResult));
                completed.Add(item.Step.Index);
                pending.Remove(item.Step.Index);
            }
        }

        return lastResult;
    }

    private async Task<StepDispatch> DispatchAsync(
        ExecutionPlanStep step,
        CancellationToken cancellationToken)
    {
        CommandDispatchResult dispatch = await dispatcher
            .TryExecuteAsync(step.Command, cancellationToken)
            .ConfigureAwait(false);
        if (!dispatch.IsHandled)
        {
            throw new CommandRouteNotFoundException(
                $"No typed route is registered for " +
                $"'{step.Command.Command.Name}/{step.Command.Frame.UsageName}'.");
        }
        return new StepDispatch(step, dispatch.Result);
    }

    private sealed record StepDispatch(ExecutionPlanStep Step, object? Result);

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
