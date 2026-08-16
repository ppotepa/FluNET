using FluNET.Execution.Commands;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Variables;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluNET.Execution.Workflow;
using FluNET.Telemetry;

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

public enum ExecutionDependencyKind { Sequence, Variable }
public sealed record ExecutionDependency(int PredecessorIndex, ExecutionDependencyKind Kind, string? Variable = null);

public enum RetryBackoffKind { Fixed, Exponential }

public sealed record RetryBackoffPolicy
{
    public RetryBackoffPolicy(
        TimeSpan baseDelay,
        RetryBackoffKind kind = RetryBackoffKind.Fixed,
        double jitterFraction = 0)
    {
        if (baseDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(baseDelay));
        if (jitterFraction is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(jitterFraction));
        BaseDelay = baseDelay;
        Kind = kind;
        JitterFraction = jitterFraction;
    }
    public TimeSpan BaseDelay { get; init; }
    public RetryBackoffKind Kind { get; init; }
    public double JitterFraction { get; init; }
}

public sealed record CommandExecutionPolicy(
    int RetryCount,
    TimeSpan? Timeout,
    WorkflowErrorBehavior ErrorBehavior,
    string? Condition,
    bool InvertCondition,
    RetryBackoffPolicy? Backoff = null,
    IReadOnlyList<int>? RetryOnStatusCodes = null,
    IReadOnlyList<int>? ContinueOnStatusCodes = null,
    IReadOnlyList<int>? FailOnStatusCodes = null)
{
    public static CommandExecutionPolicy Default { get; } = new(0, null, WorkflowErrorBehavior.Fail, null, false);
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

public sealed class ExecutionPlan
{
    private readonly ReadOnlyCollection<ExecutionPlanStep> _steps;
    private readonly ReadOnlyCollection<VariableSymbol> _variables;
    internal ExecutionPlan(IEnumerable<ExecutionPlanStep> steps, IEnumerable<VariableSymbol>? variables = null)
    {
        _steps = Array.AsReadOnly(steps.ToArray());
        _variables = Array.AsReadOnly(variables?.ToArray() ?? Array.Empty<VariableSymbol>());
    }
    public IReadOnlyList<ExecutionPlanStep> Steps => _steps;
    public IReadOnlyList<VariableSymbol> Variables => _variables;
}

public sealed class ExecutionPlanner
{
    private const int MaximumRetryCount = 100;

    public ExecutionPlan Create(IReadOnlyList<BoundCommand> commands, Prompt.PromptSyntax? syntax = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (syntax is not null && syntax.Commands.Count != commands.Count)
            throw new ExecutionPlanException("Syntax and semantic command counts do not match.");

        List<ExecutionPlanStep> steps = [];
        Dictionary<string, int> producers = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, VariableSymbol> symbols = new(StringComparer.OrdinalIgnoreCase);
        int[] stages = BuildStages(commands.Count, syntax);
        CommandExecutionPolicy[] policies = commands.Select(ParsePolicy).ToArray();
        ApplyAlternatives(policies, syntax);

        for (int index = 0; index < commands.Count; index++)
        {
            BoundCommand command = commands[index];
            ResultBinding? resultBinding = FindResultBinding(command);
            List<ExecutionDependency> dependencies = stages[index] == 0
                ? []
                : Enumerable.Range(0, index)
                    .Where(predecessor => stages[predecessor] == stages[index] - 1)
                    .Select(predecessor => new ExecutionDependency(predecessor, ExecutionDependencyKind.Sequence))
                    .ToList();
            foreach (string variable in FindInputVariables(command)) AddVariableDependency(variable, producers, dependencies);
            foreach (string variable in FindConditionVariables(policies[index])) AddVariableDependency(variable, producers, dependencies);
            steps.Add(new ExecutionPlanStep(index, command, resultBinding, dependencies, policies[index]));
            if (resultBinding is not null)
            {
                resultBinding.Type = command.Frame.ResultTypeSymbol;
                foreach (string target in resultBinding.Targets)
                {
                    producers[target] = index;
                    symbols[target] = new VariableSymbol(target, resultBinding.Type, index);
                }
            }
        }
        return new ExecutionPlan(steps, symbols.Values.OrderBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static CommandExecutionPolicy ParsePolicy(BoundCommand command)
    {
        CommandExecutionPolicy policy = ParseSyntaxPolicy(command.Syntax);
        if (CommandExecutionArtifactStore.TryGetAdvancedPolicy(command, out AdvancedExecutionPolicy? advanced) && advanced is not null)
        {
            policy = policy with
            {
                Backoff = advanced.Backoff,
                RetryOnStatusCodes = advanced.RetryOnStatusCodes,
                ContinueOnStatusCodes = advanced.ContinueOnStatusCodes,
                FailOnStatusCodes = advanced.FailOnStatusCodes
            };
        }
        return policy;
    }

    private static CommandExecutionPolicy ParseSyntaxPolicy(Prompt.CommandSyntax syntax)
    {
        CommandExecutionPolicy policy = CommandExecutionPolicy.Default;
        HashSet<Prompt.CommandModifierKind> seen = [];
        foreach (Prompt.CommandModifierSyntax modifier in syntax.Modifiers)
        {
            if (!seen.Add(modifier.Kind))
                throw new ExecutionPlanException($"Modifier '{modifier.Kind}' is declared more than once on one command.");
            policy = modifier.Kind switch
            {
                Prompt.CommandModifierKind.Retry => policy with { RetryCount = ParseRetry(SingleModifierValue(modifier)) },
                Prompt.CommandModifierKind.Timeout => policy with { Timeout = ParseTimeout(SingleModifierValue(modifier)) },
                Prompt.CommandModifierKind.ErrorPolicy => policy with
                {
                    ErrorBehavior = SingleModifierValue(modifier).ToUpperInvariant() switch
                    {
                        "CONTINUE" => WorkflowErrorBehavior.Continue,
                        "FAIL" or "STOP" => WorkflowErrorBehavior.Fail,
                        string value => throw new ExecutionPlanException($"Unknown ON ERROR behavior '{value}'. Expected CONTINUE or FAIL.")
                    }
                },
                Prompt.CommandModifierKind.Condition => policy with { Condition = ConditionSource(modifier) },
                _ => throw new ExecutionPlanException($"Unknown command modifier '{modifier.Kind}'.")
            };
        }
        return policy;
    }

    private static void AddVariableDependency(string variable, IReadOnlyDictionary<string, int> producers, ICollection<ExecutionDependency> dependencies)
    {
        if (!producers.TryGetValue(variable, out int producer) ||
            dependencies.Any(dependency => dependency.PredecessorIndex == producer && dependency.Kind == ExecutionDependencyKind.Variable && dependency.Variable?.Equals(variable, StringComparison.OrdinalIgnoreCase) == true))
            return;
        dependencies.Add(new ExecutionDependency(producer, ExecutionDependencyKind.Variable, variable));
    }

    private static IEnumerable<string> FindInputVariables(BoundCommand command) =>
        command.Arguments.Values
            .Where(argument => argument.Slot.Direction == SlotDirection.Input)
            .SelectMany(argument => argument.Tokens)
            .Where(token => token.Kind == Prompt.PromptTokenKind.Variable)
            .Select(token => NormalizeVariableReference(token.Text))
            .Where(name => !name.StartsWith('{'))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> FindConditionVariables(CommandExecutionPolicy policy) =>
        string.IsNullOrWhiteSpace(policy.Condition)
            ? Array.Empty<string>()
            : ConditionExpressionCache.GetOrCompile(policy.Condition).VariableReferences.OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeVariableReference(string reference)
    {
        string normalized = reference.TrimEnd('.');
        return normalized.Length >= 2 && normalized[0] == '[' && normalized[^1] == ']'
            ? normalized[1..^1]
            : normalized;
    }

    private static int[] BuildStages(int commandCount, Prompt.PromptSyntax? syntax)
    {
        int[] stages = new int[commandCount];
        if (commandCount < 2) return stages;
        if (syntax is null || syntax.Links.Count == 0)
        {
            for (int index = 1; index < commandCount; index++) stages[index] = index;
            return stages;
        }
        Dictionary<int, Prompt.CommandLinkSyntax> links = syntax.Links.ToDictionary(link => link.SuccessorIndex);
        for (int index = 1; index < commandCount; index++)
        {
            if (!links.TryGetValue(index, out Prompt.CommandLinkSyntax? link))
            {
                stages[index] = stages[index - 1];
                continue;
            }
            stages[index] = stages[index - 1] + (link.Kind == Prompt.CommandLinkKind.Sequence ? 1 : 0);
        }
        return stages;
    }

    private static string SingleModifierValue(Prompt.CommandModifierSyntax modifier)
    {
        if (modifier.Values.Count != 1)
            throw new ExecutionPlanException($"Modifier '{modifier.Kind}' requires exactly one value.");
        return Unwrap(modifier.Values[0].Text);
    }

    private static string ConditionSource(Prompt.CommandModifierSyntax modifier)
    {
        if (modifier.Values.Count == 0) throw new ExecutionPlanException("IF must be followed by a condition expression.");
        return string.Join(" ", modifier.Values.Select(token => token.Text));
    }

    private static void ApplyAlternatives(CommandExecutionPolicy[] policies, Prompt.PromptSyntax? syntax)
    {
        if (syntax is null) return;
        foreach (Prompt.CommandLinkSyntax link in syntax.Links.Where(link => link.Kind == Prompt.CommandLinkKind.Alternative))
        {
            CommandExecutionPolicy predecessor = policies[link.PredecessorIndex];
            if (predecessor.Condition is null) throw new ExecutionPlanException("ELSE requires the previous command to declare IF.");
            if (policies[link.SuccessorIndex].Condition is not null) throw new ExecutionPlanException("An ELSE command cannot declare another IF condition.");
            policies[link.SuccessorIndex] = policies[link.SuccessorIndex] with
            {
                Condition = predecessor.Condition,
                InvertCondition = !predecessor.InvertCondition
            };
        }
    }

    private static int ParseRetry(string value) =>
        int.TryParse(value, out int retries) && retries is >= 0 and <= MaximumRetryCount
            ? retries
            : throw new ExecutionPlanException($"Retry count '{value}' must be between 0 and {MaximumRetryCount}.");

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
        if (!double.TryParse(parts.Number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number) || number <= 0)
            throw new ExecutionPlanException($"Timeout '{value}' must be a positive duration.");
        double milliseconds = number * parts.Multiplier;
        if (!double.IsFinite(milliseconds) || milliseconds > int.MaxValue)
            throw new ExecutionPlanException($"Timeout '{value}' is too large.");
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static string Unwrap(string value) =>
        value.Length >= 2 && ((value[0] == '{' && value[^1] == '}') || (value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

    private static ResultBinding? FindResultBinding(BoundCommand command)
    {
        BoundArgument? output = command.Arguments.Values.SingleOrDefault(argument => argument.Slot.Direction == SlotDirection.Output);
        if (output?.Tokens.Count != 1 || output.Tokens[0].Kind != Prompt.PromptTokenKind.Variable) return null;
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

/// <summary>Executes an already planned FluNET program.</summary>
/// <summary>
/// Executes the typed sentence plan produced by the compiler.
/// A sentence is the semantic unit of a FluNET program; this class owns only
/// execution and has no dependency on parser implementation details.
/// </summary>
public class SentenceExecutor
{
    private readonly CommandDispatcher dispatcher;
    private readonly IVariableResolver variables;
    private readonly IWorkflowStateStore stateStore;
    private readonly IWorkflowValueSerializer valueSerializer;
    private readonly IFluNetTelemetrySink telemetry;

    public SentenceExecutor(
        CommandDispatcher dispatcher,
        IVariableResolver variables,
        IWorkflowStateStore stateStore,
        IWorkflowValueSerializer valueSerializer,
        IFluNetTelemetrySink telemetry)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.variables = variables ?? throw new ArgumentNullException(nameof(variables));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.valueSerializer = valueSerializer ?? throw new ArgumentNullException(nameof(valueSerializer));
        this.telemetry = telemetry ?? NullFluNetTelemetrySink.Instance;
    }
    public SentenceExecutor(CommandDispatcher dispatcher, IVariableResolver variables, IWorkflowStateStore stateStore, IWorkflowValueSerializer valueSerializer)
        : this(dispatcher, variables, stateStore, valueSerializer, NullFluNetTelemetrySink.Instance) { }
    public SentenceExecutor(CommandDispatcher dispatcher, IVariableResolver variables, IWorkflowStateStore stateStore)
        : this(dispatcher, variables, stateStore, new JsonWorkflowValueSerializer()) { }
    public SentenceExecutor(CommandDispatcher dispatcher, IVariableResolver variables)
        : this(dispatcher, variables, new InMemoryWorkflowStateStore(), new JsonWorkflowValueSerializer()) { }

    public ValueTask<object?> ExecuteAsync(ExecutionPlan plan, ICollection<ExecutionStepResult> completedSteps, CancellationToken cancellationToken = default) =>
        ExecuteAsync(plan, completedSteps, new WorkflowRunState(), cancellationToken);

    public async ValueTask<object?> ExecuteAsync(ExecutionPlan plan, ICollection<ExecutionStepResult> completedSteps, WorkflowRunState workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(completedSteps);
        ArgumentNullException.ThrowIfNull(workflow);
        await EmitRunAsync(workflow, "started", plan.Steps.Count, 0).ConfigureAwait(false);
        foreach ((string name, object? value) in workflow.Inputs)
        {
            if (value is not null)
                variables.Register(name, value);
        }
        workflow.BindPlan(CreatePlanFingerprint(plan));
        object? lastResult = null;
        HashSet<int> completed = [];
        Dictionary<int, ExecutionPlanStep> pending = plan.Steps.ToDictionary(step => step.Index);
        IReadOnlyList<WorkflowEvent> history = workflow.Options.RunId is null
            ? Array.Empty<WorkflowEvent>()
            : await stateStore.ReadAsync(workflow.RunId, cancellationToken).ConfigureAwait(false);
        if (workflow.Options.Resume)
        {
            if (workflow.Options.RunId is null) throw new WorkflowResumeException("Resume requires an explicit workflow run identifier.");
            lastResult = await RestoreAsync(history, pending, completed, completedSteps, workflow, cancellationToken).ConfigureAwait(false);
        }
        else if (history.Count > 0)
        {
            throw new WorkflowResumeException($"Workflow run '{workflow.RunId}' already exists. Use Resume=true or a new run identifier.");
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionPlanStep[] ready = pending.Values
                .Where(step => step.Dependencies.All(dependency => completed.Contains(dependency.PredecessorIndex)))
                .OrderBy(step => step.Index)
                .ToArray();
            if (ready.Length == 0) throw new ExecutionPlanException("The execution graph contains an unsatisfied dependency cycle.");
            StepDispatch[] dispatched = await Task.WhenAll(ready.Select(step => DispatchAsync(step, workflow, cancellationToken))).ConfigureAwait(false);
            foreach (StepDispatch item in dispatched.OrderBy(item => item.Step.Index))
            {
                if (item.Status == WorkflowStepStatus.Succeeded)
                {
                    lastResult = item.Result;
                    if (lastResult is not null && item.Step.ResultBinding is not null) Store(item.Step.ResultBinding, lastResult);
                }
                completedSteps.Add(new ExecutionStepResult(item.Step, item.Result, item.Status, item.Attempts, item.Error));
                completed.Add(item.Step.Index);
                pending.Remove(item.Step.Index);
            }
            StepDispatch? fatal = dispatched.Where(item => item.IsFatal).OrderBy(item => item.Step.Index).FirstOrDefault();
            if (fatal?.Error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(fatal.Error).Throw();
        }
        workflow.Complete();
        await EmitRunAsync(workflow, "succeeded", plan.Steps.Count, completed.Count).ConfigureAwait(false);
        return lastResult;
    }

    private async Task<StepDispatch> DispatchAsync(ExecutionPlanStep step, WorkflowRunState workflow, CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            if (!EvaluateCondition(step.Policy))
            {
                await RecordAsync(workflow, step.Index, WorkflowStepStatus.Skipped, 0, "Condition evaluated to false.", null, cancellationToken).ConfigureAwait(false);
                await EmitStepAsync(step, WorkflowStepStatus.Skipped, 0, started).ConfigureAwait(false);
                return new StepDispatch(step, null, WorkflowStepStatus.Skipped, 0, null, false);
            }
        }
        catch (Exception exception)
        {
            await RecordAsync(workflow, step.Index, WorkflowStepStatus.Failed, 0, exception.Message, null, cancellationToken).ConfigureAwait(false);
            await EmitStepAsync(step, WorkflowStepStatus.Failed, 0, started, exception).ConfigureAwait(false);
            return new StepDispatch(step, null, WorkflowStepStatus.Failed, 0, exception, EffectiveErrorBehavior(step.Policy, exception) == WorkflowErrorBehavior.Fail);
        }

        Exception? lastError = null;
        int maxAttempts = checked(step.Policy.RetryCount + 1);
        int attempts = 0;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            attempts = attempt;
            lastError = null;
            await RecordAsync(workflow, step.Index, WorkflowStepStatus.Running, attempt, null, null, cancellationToken).ConfigureAwait(false);
            await EmitStepAsync(step, WorkflowStepStatus.Running, attempt, started).ConfigureAwait(false);
            using CancellationTokenSource? timeout = step.Policy.Timeout is null ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout is not null && step.Policy.Timeout is TimeSpan timeoutDelay) timeout.CancelAfter(timeoutDelay);
            CancellationToken stepToken = timeout?.Token ?? cancellationToken;

            CommandDispatchResult dispatch = default;
            try
            {
                dispatch = await dispatcher.TryExecuteAsync(step.Command, stepToken).ConfigureAwait(false);
                if (!dispatch.IsHandled)
                    throw new CommandRouteNotFoundException($"No typed route is registered for '{step.Command.Command.Name}/{step.Command.Frame.UsageName}'.");
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && timeout?.IsCancellationRequested == true)
            {
                lastError = new WorkflowTimeoutException($"Execution step {step.Index} exceeded timeout {step.Policy.Timeout}.", exception);
            }
            catch (Exception exception) { lastError = exception; }

            if (lastError is null)
            {
                string? resultJson = valueSerializer.Serialize(dispatch.Result, step.Command.Frame.ResultType);
                await RecordAsync(workflow, step.Index, WorkflowStepStatus.Succeeded, attempt, null, resultJson, cancellationToken).ConfigureAwait(false);
                await EmitStepAsync(step, WorkflowStepStatus.Succeeded, attempt, started).ConfigureAwait(false);
                return new StepDispatch(step, dispatch.Result, WorkflowStepStatus.Succeeded, attempt, null, false);
            }

            if (attempt < maxAttempts && ShouldRetry(step.Policy, lastError))
            {
                await RecordAsync(workflow, step.Index, WorkflowStepStatus.Retrying, attempt, lastError.Message, null, cancellationToken).ConfigureAwait(false);
                await EmitStepAsync(step, WorkflowStepStatus.Retrying, attempt, started, lastError).ConfigureAwait(false);
                TimeSpan delay = BackoffDelay(step.Policy.Backoff, step.Index, attempt);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            else break;
        }

        await RecordAsync(workflow, step.Index, WorkflowStepStatus.Failed, attempts, lastError?.Message, null, cancellationToken).ConfigureAwait(false);
        Exception error = lastError ?? new InvalidOperationException($"Execution step {step.Index} failed.");
        await EmitStepAsync(step, WorkflowStepStatus.Failed, attempts, started, error).ConfigureAwait(false);
        return new StepDispatch(step, null, WorkflowStepStatus.Failed, attempts, error, EffectiveErrorBehavior(step.Policy, error) == WorkflowErrorBehavior.Fail);
    }

    private ValueTask EmitRunAsync(WorkflowRunState workflow, string outcome, int planSteps, int completedSteps) =>
        FluNetTelemetry.TryEmitAsync(telemetry, new(
            DateTimeOffset.UtcNow,
            "execution",
            "run",
            outcome,
            TimeSpan.Zero,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["run.id"] = workflow.RunId.ToString("N"),
                ["plan.steps"] = planSteps.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["completed.steps"] = completedSteps.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));

    private ValueTask EmitStepAsync(
        ExecutionPlanStep step,
        WorkflowStepStatus status,
        int attempt,
        long started,
        Exception? error = null)
    {
        Dictionary<string, string> attributes = new(StringComparer.Ordinal)
        {
            ["step.index"] = step.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["frame.id"] = step.Command.Frame.Id.Value,
            ["usage"] = step.Command.Frame.UsageName,
            ["status"] = status.ToString(),
            ["attempt"] = attempt.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (error is not null) attributes["error.type"] = error.GetType().FullName ?? error.GetType().Name;
        return FluNetTelemetry.TryEmitAsync(telemetry, new(
            DateTimeOffset.UtcNow,
            "execution",
            "step",
            status.ToString().ToLowerInvariant(),
            Stopwatch.GetElapsedTime(started),
            attributes));
    }

    private static bool ShouldRetry(CommandExecutionPolicy policy, Exception exception)
    {
        if (!IsRetryable(exception)) return false;
        int? status = StatusCode(exception);
        if (status is int fail && policy.FailOnStatusCodes?.Contains(fail) == true) return false;
        if (policy.RetryOnStatusCodes is { Count: > 0 })
            return status is int retry && policy.RetryOnStatusCodes.Contains(retry);
        return true;
    }

    private static WorkflowErrorBehavior EffectiveErrorBehavior(CommandExecutionPolicy policy, Exception exception)
    {
        int? status = StatusCode(exception);
        if (status is int code)
        {
            if (policy.FailOnStatusCodes?.Contains(code) == true) return WorkflowErrorBehavior.Fail;
            if (policy.ContinueOnStatusCodes?.Contains(code) == true) return WorkflowErrorBehavior.Continue;
        }
        return policy.ErrorBehavior;
    }

    private static int? StatusCode(Exception exception) =>
        exception is HttpRequestException http && http.StatusCode is System.Net.HttpStatusCode status
            ? (int)status
            : null;

    private static TimeSpan BackoffDelay(RetryBackoffPolicy? policy, int stepIndex, int attempt)
    {
        if (policy is null) return TimeSpan.Zero;
        double scale = policy.Kind == RetryBackoffKind.Exponential ? Math.Pow(2, Math.Min(20, attempt - 1)) : 1;
        double milliseconds = Math.Min(TimeSpan.FromDays(1).TotalMilliseconds, policy.BaseDelay.TotalMilliseconds * scale);
        if (policy.JitterFraction > 0)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{stepIndex}:{attempt}"));
            double unit = BitConverter.ToUInt32(hash, 0) / (double)uint.MaxValue;
            double factor = 1 + ((unit * 2) - 1) * policy.JitterFraction;
            milliseconds *= factor;
        }
        return TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
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
        if (history.Any(item => !string.Equals(item.PlanFingerprint, workflow.PlanFingerprint, StringComparison.Ordinal)))
            throw new WorkflowResumeException($"Workflow run '{workflow.RunId}' belongs to a different execution plan.");
        foreach (WorkflowEvent item in history) workflow.Record(item);
        object? lastResult = null;
        foreach (IGrouping<int, WorkflowEvent> group in history.GroupBy(item => item.StepIndex).OrderBy(group => group.Key))
        {
            if (!pending.TryGetValue(group.Key, out ExecutionPlanStep? step)) continue;
            WorkflowEvent item = group.Last();
            bool restorable = item.Status is WorkflowStepStatus.Succeeded or WorkflowStepStatus.Skipped ||
                item.Status == WorkflowStepStatus.Failed && step.Policy.ErrorBehavior == WorkflowErrorBehavior.Continue;
            if (!restorable) continue;
            object? result = item.Status == WorkflowStepStatus.Succeeded
                ? valueSerializer.Deserialize(item.ResultJson, step.Command.Frame.ResultType)
                : null;
            if (result is not null && step.ResultBinding is not null) Store(step.ResultBinding, result);
            completed.Add(step.Index);
            pending.Remove(step.Index);
            completedSteps.Add(new ExecutionStepResult(
                step,
                result,
                item.Status,
                item.Attempt,
                item.Status == WorkflowStepStatus.Failed
                    ? new WorkflowRestoredFailureException(item.Message ?? $"Workflow step {step.Index} previously failed.")
                    : null));
            if (item.Status == WorkflowStepStatus.Succeeded) lastResult = result;
        }
        return ValueTask.FromResult(lastResult);
    }

    private bool EvaluateCondition(CommandExecutionPolicy policy)
    {
        if (policy.Condition is null) return true;
        CompiledCondition condition = ConditionExpressionCache.GetOrCompile(policy.Condition);
        bool result = condition.Expression.Evaluate(new ExpressionEvaluationContext(variables));
        return policy.InvertCondition ? !result : result;
    }

    private async ValueTask RecordAsync(WorkflowRunState workflow, int stepIndex, WorkflowStepStatus status, int attempt, string? message, string? resultJson, CancellationToken cancellationToken)
    {
        WorkflowEvent item = new(workflow.RunId, stepIndex, status, attempt, DateTimeOffset.UtcNow, message, resultJson, workflow.PlanFingerprint);
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
            Dependencies = step.Dependencies.Select(dependency => new { dependency.PredecessorIndex, dependency.Kind, dependency.Variable }),
            ResultTargets = step.ResultBinding?.Targets,
            step.Policy
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record StepDispatch(ExecutionPlanStep Step, object? Result, WorkflowStepStatus Status, int Attempts, Exception? Error, bool IsFatal);

    private void Store(ResultBinding binding, object result)
    {
        if (!binding.IsDestructuring) { variables.Register(binding.Targets[0], result); return; }
        IReadOnlyDictionary<string, object> properties = ExtractProperties(result);
        foreach (string target in binding.Targets) if (properties.TryGetValue(target, out object? value)) variables.Register(target, value);
    }

    private static IReadOnlyDictionary<string, object> ExtractProperties(object result)
    {
        if (result is IReadOnlyDictionary<string, object> readOnly) return new Dictionary<string, object>(readOnly, StringComparer.OrdinalIgnoreCase);
        string? json = result switch { string value => value, string[] lines => string.Join('\n', lines), JsonElement element => element.GetRawText(), _ => null };
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => ExtractJsonValue(property.Value), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); }
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
public sealed class WorkflowTimeoutException(string message, Exception? innerException = null) : TimeoutException(message, innerException);
public sealed class WorkflowResumeException(string message) : Exception(message);
public sealed class WorkflowRestoredFailureException(string message) : Exception(message);
