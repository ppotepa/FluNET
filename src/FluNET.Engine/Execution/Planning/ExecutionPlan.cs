using FluNET.Execution.Commands;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Variables;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluNET.Execution.Workflow;

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

public sealed record CommandExecutionPolicy(
    int RetryCount,
    TimeSpan? Timeout,
    WorkflowErrorBehavior ErrorBehavior,
    string? Condition,
    bool InvertCondition)
{
    public static CommandExecutionPolicy Default { get; } =
        new(0, null, WorkflowErrorBehavior.Fail, null, false);
}

public sealed class ExecutionPlanStep
{
    private readonly ReadOnlyCollection<ExecutionDependency> _dependencies;

    internal ExecutionPlanStep(
        int index,
        BoundCommand command,
        ResultBinding? resultBinding,
        IEnumerable<ExecutionDependency> dependencies,
        CommandExecutionPolicy? policy = null)
    {
        Index = index;
        Command = command;
        ResultBinding = resultBinding;
        _dependencies = Array.AsReadOnly(dependencies.ToArray());
        Policy = policy ?? CommandExecutionPolicy.Default;
    }

    public int Index { get; }
    public BoundCommand Command { get; }
    public ResultBinding? ResultBinding { get; }
    public IReadOnlyList<ExecutionDependency> Dependencies => _dependencies;
    public CommandExecutionPolicy Policy { get; }
}

/// <summary>An immutable orchestration graph with explicit control and data dependencies.</summary>
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
        CommandExecutionPolicy[] policies = commands
            .Select(command => ParsePolicy(command.Syntax))
            .ToArray();
        ApplyAlternatives(policies, syntax);

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

            string? conditionVariable = FindConditionVariable(policies[index]);
            if (conditionVariable is not null &&
                producers.TryGetValue(conditionVariable, out int conditionProducer) &&
                !dependencies.Any(dependency =>
                    dependency.PredecessorIndex == conditionProducer &&
                    dependency.Kind == ExecutionDependencyKind.Variable &&
                    dependency.Variable?.Equals(
                        conditionVariable,
                        StringComparison.OrdinalIgnoreCase) == true))
            {
                dependencies.Add(new ExecutionDependency(
                    conditionProducer,
                    ExecutionDependencyKind.Variable,
                    conditionVariable));
            }

            steps.Add(new ExecutionPlanStep(
                index,
                command,
                resultBinding,
                dependencies,
                policies[index]));
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

    private static string? FindConditionVariable(CommandExecutionPolicy policy)
    {
        string? reference = policy.Condition?.TrimEnd('.');
        return reference is not null &&
            reference.Length >= 2 &&
            reference[0] == '[' &&
            reference[^1] == ']'
                ? reference[1..^1]
                : null;
    }

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

    private static CommandExecutionPolicy ParsePolicy(Prompt.CommandSyntax syntax)
    {
        CommandExecutionPolicy policy = CommandExecutionPolicy.Default;
        HashSet<Prompt.CommandModifierKind> seen = [];
        foreach (Prompt.CommandModifierSyntax modifier in syntax.Modifiers)
        {
            if (!seen.Add(modifier.Kind))
            {
                throw new ExecutionPlanException(
                    $"Modifier '{modifier.Kind}' is declared more than once on one command.");
            }
            if (modifier.Values.Count != 1)
            {
                throw new ExecutionPlanException(
                    $"Modifier '{modifier.Kind}' requires exactly one value.");
            }

            string value = Unwrap(modifier.Values[0].Text);
            policy = modifier.Kind switch
            {
                Prompt.CommandModifierKind.Retry => policy with
                {
                    RetryCount = ParseRetry(value)
                },
                Prompt.CommandModifierKind.Timeout => policy with
                {
                    Timeout = ParseTimeout(value)
                },
                Prompt.CommandModifierKind.ErrorPolicy => policy with
                {
                    ErrorBehavior = value.ToUpperInvariant() switch
                    {
                        "CONTINUE" => WorkflowErrorBehavior.Continue,
                        "FAIL" or "STOP" => WorkflowErrorBehavior.Fail,
                        _ => throw new ExecutionPlanException(
                            $"Unknown ON ERROR behavior '{value}'. Expected CONTINUE or FAIL.")
                    }
                },
                Prompt.CommandModifierKind.Condition => policy with
                {
                    Condition = modifier.Values[0].Text
                },
                _ => throw new ExecutionPlanException($"Unknown command modifier '{modifier.Kind}'.")
            };
        }
        return policy;
    }

    private static void ApplyAlternatives(
        CommandExecutionPolicy[] policies,
        Prompt.PromptSyntax? syntax)
    {
        if (syntax is null)
        {
            return;
        }
        foreach (Prompt.CommandLinkSyntax link in syntax.Links
            .Where(link => link.Kind == Prompt.CommandLinkKind.Alternative))
        {
            CommandExecutionPolicy predecessor = policies[link.PredecessorIndex];
            if (predecessor.Condition is null)
            {
                throw new ExecutionPlanException("ELSE requires the previous command to declare IF.");
            }
            if (policies[link.SuccessorIndex].Condition is not null)
            {
                throw new ExecutionPlanException("An ELSE command cannot declare another IF condition.");
            }
            policies[link.SuccessorIndex] = policies[link.SuccessorIndex] with
            {
                Condition = predecessor.Condition,
                InvertCondition = !predecessor.InvertCondition
            };
        }
    }

    private static int ParseRetry(string value) =>
        int.TryParse(value, out int retries) && retries >= 0
            ? retries
            : throw new ExecutionPlanException($"Retry count '{value}' must be a non-negative integer.");

    private static TimeSpan ParseTimeout(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        (string Number, double Multiplier) parts = normalized switch
        {
            _ when normalized.EndsWith("ms") => (normalized[..^2], 1),
            _ when normalized.EndsWith('s') => (normalized[..^1], 1_000),
            _ when normalized.EndsWith('m') => (normalized[..^1], 60_000),
            _ when normalized.EndsWith('h') => (normalized[..^1], 3_600_000),
            _ => (normalized, 1_000)
        };
        return double.TryParse(
            parts.Number,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double number) && number > 0
                ? TimeSpan.FromMilliseconds(number * parts.Multiplier)
                : throw new ExecutionPlanException($"Timeout '{value}' must be a positive duration.");
    }

    private static string Unwrap(string value) =>
        value.Length >= 2 &&
        ((value[0] == '{' && value[^1] == '}') ||
         (value[0] == '"' && value[^1] == '"') ||
         (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

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

public sealed record ExecutionStepResult(
    ExecutionPlanStep Step,
    object? Result,
    WorkflowStepStatus Status = WorkflowStepStatus.Succeeded,
    int Attempts = 1,
    Exception? Error = null);

public sealed class ExecutionPlanExecutor
{
    private readonly CommandDispatcher dispatcher;
    private readonly IVariableResolver variables;
    private readonly IWorkflowStateStore stateStore;
    private readonly IWorkflowValueSerializer valueSerializer;

    public ExecutionPlanExecutor(
        CommandDispatcher dispatcher,
        IVariableResolver variables,
        IWorkflowStateStore stateStore,
        IWorkflowValueSerializer valueSerializer)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.variables = variables ?? throw new ArgumentNullException(nameof(variables));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.valueSerializer = valueSerializer ?? throw new ArgumentNullException(nameof(valueSerializer));
    }

    public ExecutionPlanExecutor(
        CommandDispatcher dispatcher,
        IVariableResolver variables,
        IWorkflowStateStore stateStore)
        : this(dispatcher, variables, stateStore, new JsonWorkflowValueSerializer())
    {
    }

    /// <summary>Compatibility constructor for hosts that create the executor directly.</summary>
    public ExecutionPlanExecutor(CommandDispatcher dispatcher, IVariableResolver variables)
        : this(
            dispatcher,
            variables,
            new InMemoryWorkflowStateStore(),
            new JsonWorkflowValueSerializer())
    {
    }

    public async ValueTask<object?> ExecuteAsync(
        ExecutionPlan plan,
        ICollection<ExecutionStepResult> completedSteps,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(
            plan,
            completedSteps,
            new WorkflowRunState(),
            cancellationToken).ConfigureAwait(false);

    public async ValueTask<object?> ExecuteAsync(
        ExecutionPlan plan,
        ICollection<ExecutionStepResult> completedSteps,
        WorkflowRunState workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(completedSteps);
        ArgumentNullException.ThrowIfNull(workflow);
        workflow.BindPlan(CreatePlanFingerprint(plan));
        object? lastResult = null;
        HashSet<int> completed = [];
        Dictionary<int, ExecutionPlanStep> pending = plan.Steps.ToDictionary(step => step.Index);

        IReadOnlyList<WorkflowEvent> history = workflow.Options.RunId is null
            ? Array.Empty<WorkflowEvent>()
            : await stateStore.ReadAsync(workflow.RunId, cancellationToken).ConfigureAwait(false);
        if (workflow.Options.Resume)
        {
            if (workflow.Options.RunId is null)
            {
                throw new WorkflowResumeException("Resume requires an explicit workflow run identifier.");
            }
            lastResult = await RestoreAsync(
                history,
                pending,
                completed,
                completedSteps,
                workflow,
                cancellationToken).ConfigureAwait(false);
        }
        else if (history.Count > 0)
        {
            throw new WorkflowResumeException(
                $"Workflow run '{workflow.RunId}' already exists. Use Resume=true or a new run identifier.");
        }

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
                DispatchAsync(step, workflow, cancellationToken))).ConfigureAwait(false);
            foreach (StepDispatch item in dispatched.OrderBy(item => item.Step.Index))
            {
                if (item.Status == WorkflowStepStatus.Succeeded)
                {
                    lastResult = item.Result;
                    if (lastResult is not null && item.Step.ResultBinding is not null)
                    {
                        Store(item.Step.ResultBinding, lastResult);
                    }
                }
                completedSteps.Add(new ExecutionStepResult(
                    item.Step,
                    item.Result,
                    item.Status,
                    item.Attempts,
                    item.Error));
                completed.Add(item.Step.Index);
                pending.Remove(item.Step.Index);
            }
            StepDispatch? fatal = dispatched
                .Where(item => item.IsFatal)
                .OrderBy(item => item.Step.Index)
                .FirstOrDefault();
            if (fatal?.Error is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(fatal.Error)
                    .Throw();
            }
        }

        workflow.Complete();
        return lastResult;
    }

    private async Task<StepDispatch> DispatchAsync(
        ExecutionPlanStep step,
        WorkflowRunState workflow,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!EvaluateCondition(step.Policy))
            {
                await RecordAsync(
                    workflow,
                    step.Index,
                    WorkflowStepStatus.Skipped,
                    0,
                    "Condition evaluated to false.",
                    null,
                    cancellationToken).ConfigureAwait(false);
                return new StepDispatch(
                    step,
                    null,
                    WorkflowStepStatus.Skipped,
                    0,
                    null,
                    false);
            }
        }
        catch (Exception exception)
        {
            await RecordAsync(
                workflow,
                step.Index,
                WorkflowStepStatus.Failed,
                0,
                exception.Message,
                null,
                cancellationToken).ConfigureAwait(false);
            return new StepDispatch(
                step,
                null,
                WorkflowStepStatus.Failed,
                0,
                exception,
                step.Policy.ErrorBehavior == WorkflowErrorBehavior.Fail);
        }

        Exception? lastError = null;
        int maxAttempts = checked(step.Policy.RetryCount + 1);
        int attempts = 0;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            attempts = attempt;
            await RecordAsync(
                workflow,
                step.Index,
                WorkflowStepStatus.Running,
                attempt,
                null,
                null,
                cancellationToken).ConfigureAwait(false);
            using CancellationTokenSource? timeout = step.Policy.Timeout is null
                ? null
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout?.CancelAfter(step.Policy.Timeout.Value);
            CancellationToken stepToken = timeout?.Token ?? cancellationToken;

            try
            {
                CommandDispatchResult dispatch = await dispatcher
                    .TryExecuteAsync(step.Command, stepToken)
                    .ConfigureAwait(false);
                if (!dispatch.IsHandled)
                {
                    throw new CommandRouteNotFoundException(
                        $"No typed route is registered for " +
                        $"'{step.Command.Command.Name}/{step.Command.Frame.UsageName}'.");
                }
                string? resultJson = valueSerializer.Serialize(
                    dispatch.Result,
                    step.Command.Frame.ResultType);
                await RecordAsync(
                    workflow,
                    step.Index,
                    WorkflowStepStatus.Succeeded,
                    attempt,
                    null,
                    resultJson,
                    cancellationToken).ConfigureAwait(false);
                return new StepDispatch(
                    step,
                    dispatch.Result,
                    WorkflowStepStatus.Succeeded,
                    attempt,
                    null,
                    false);
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested && timeout?.IsCancellationRequested == true)
            {
                lastError = new WorkflowTimeoutException(
                    $"Execution step {step.Index} exceeded timeout {step.Policy.Timeout}.",
                    exception);
            }
            catch (Exception exception)
            {
                lastError = exception;
            }

            if (attempt < maxAttempts && IsRetryable(lastError))
            {
                await RecordAsync(
                    workflow,
                    step.Index,
                    WorkflowStepStatus.Retrying,
                    attempt,
                    lastError.Message,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                break;
            }
        }

        await RecordAsync(
            workflow,
            step.Index,
            WorkflowStepStatus.Failed,
            attempts,
            lastError?.Message,
            null,
            cancellationToken).ConfigureAwait(false);
        Exception error = lastError ??
            new InvalidOperationException($"Execution step {step.Index} failed.");
        return new StepDispatch(
            step,
            null,
            WorkflowStepStatus.Failed,
            attempts,
            error,
            step.Policy.ErrorBehavior == WorkflowErrorBehavior.Fail);
    }

    private ValueTask<object?> RestoreAsync(
        IReadOnlyList<WorkflowEvent> history,
        IDictionary<int, ExecutionPlanStep> pending,
        ISet<int> completed,
        ICollection<ExecutionStepResult> completedSteps,
        WorkflowRunState workflow,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (history.Any(item =>
            !string.Equals(
                item.PlanFingerprint,
                workflow.PlanFingerprint,
                StringComparison.Ordinal)))
        {
            throw new WorkflowResumeException(
                $"Workflow run '{workflow.RunId}' belongs to a different execution plan.");
        }
        foreach (WorkflowEvent item in history)
        {
            workflow.Record(item);
        }

        object? lastResult = null;
        foreach (IGrouping<int, WorkflowEvent> group in history
            .GroupBy(item => item.StepIndex)
            .OrderBy(group => group.Key))
        {
            if (!pending.TryGetValue(group.Key, out ExecutionPlanStep? step))
            {
                continue;
            }
            WorkflowEvent item = group.Last();
            bool restorable = item.Status is WorkflowStepStatus.Succeeded or WorkflowStepStatus.Skipped ||
                item.Status == WorkflowStepStatus.Failed &&
                step.Policy.ErrorBehavior == WorkflowErrorBehavior.Continue;
            if (!restorable)
            {
                continue;
            }
            object? result = item.Status == WorkflowStepStatus.Succeeded
                ? valueSerializer.Deserialize(item.ResultJson, step.Command.Frame.ResultType)
                : null;
            if (result is not null && step.ResultBinding is not null)
            {
                Store(step.ResultBinding, result);
            }
            completed.Add(step.Index);
            pending.Remove(step.Index);
            completedSteps.Add(new ExecutionStepResult(
                step,
                result,
                item.Status,
                item.Attempt,
                item.Status == WorkflowStepStatus.Failed
                    ? new WorkflowRestoredFailureException(
                        item.Message ?? $"Workflow step {step.Index} previously failed.")
                    : null));
            if (item.Status == WorkflowStepStatus.Succeeded)
            {
                lastResult = result;
            }
        }
        return ValueTask.FromResult(lastResult);
    }

    private bool EvaluateCondition(CommandExecutionPolicy policy)
    {
        if (policy.Condition is null)
        {
            return true;
        }
        string condition = policy.Condition.TrimEnd('.');
        object value = condition.StartsWith('[') && condition.EndsWith(']')
            ? variables.Resolve<object>(condition)
                ?? throw new InvalidOperationException($"Condition variable {condition} not found.")
            : Unwrap(condition);
        bool result = value switch
        {
            bool boolean => boolean,
            string text when bool.TryParse(text, out bool boolean) => boolean,
            string text when decimal.TryParse(text, out decimal number) => number != 0,
            string text => !string.IsNullOrWhiteSpace(text),
            int integer => integer != 0,
            decimal number => number != 0,
            _ => true
        };
        return policy.InvertCondition ? !result : result;
    }

    private async ValueTask RecordAsync(
        WorkflowRunState workflow,
        int stepIndex,
        WorkflowStepStatus status,
        int attempt,
        string? message,
        string? resultJson,
        CancellationToken cancellationToken)
    {
        WorkflowEvent item = new(
            workflow.RunId,
            stepIndex,
            status,
            attempt,
            DateTimeOffset.UtcNow,
            message,
            resultJson,
            workflow.PlanFingerprint);
        workflow.Record(item);
        await stateStore.AppendAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsRetryable(Exception? exception) =>
        exception is not null &&
        exception is not CommandRouteNotFoundException &&
        exception is not CapabilityDeniedException &&
        exception is not OperationCanceledException;

    private static string CreatePlanFingerprint(ExecutionPlan plan)
    {
        string canonical = JsonSerializer.Serialize(plan.Steps.Select(step => new
        {
            step.Index,
            Command = step.Command.Command.Name,
            Frame = step.Command.Frame.UsageName,
            ResultType = step.Command.Frame.ResultType.AssemblyQualifiedName,
            Tokens = step.Command.Syntax.AllTokens.Select(token => token.Text),
            Dependencies = step.Dependencies.Select(dependency => new
            {
                dependency.PredecessorIndex,
                dependency.Kind,
                dependency.Variable
            }),
            ResultTargets = step.ResultBinding?.Targets,
            step.Policy
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Unwrap(string value) =>
        value.Length >= 2 &&
        ((value[0] == '{' && value[^1] == '}') ||
         (value[0] == '"' && value[^1] == '"') ||
         (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

    private sealed record StepDispatch(
        ExecutionPlanStep Step,
        object? Result,
        WorkflowStepStatus Status,
        int Attempts,
        Exception? Error,
        bool IsFatal);

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

public sealed class WorkflowTimeoutException(string message, Exception? innerException = null)
    : TimeoutException(message, innerException);

public sealed class WorkflowResumeException(string message) : Exception(message);

public sealed class WorkflowRestoredFailureException(string message) : Exception(message);
