using FluNET.Capabilities;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FluNET.Execution.Workflow;

public enum WorkflowStepStatus
{
    Pending,
    Running,
    Retrying,
    Succeeded,
    Failed,
    Skipped
}

public enum WorkflowErrorBehavior
{
    Fail,
    Continue
}

public sealed record WorkflowExecutionOptions(Guid? RunId = null, bool Resume = false);

public sealed record WorkflowEvent(
    Guid RunId,
    int StepIndex,
    WorkflowStepStatus Status,
    int Attempt,
    DateTimeOffset Timestamp,
    string? Message = null,
    string? ResultJson = null,
    string? PlanFingerprint = null);

public sealed class WorkflowRunState
{
    private readonly object _gate = new();
    private readonly List<WorkflowEvent> _events = [];
    private readonly Dictionary<string, object?> _inputs = new(StringComparer.OrdinalIgnoreCase);

    public WorkflowRunState(WorkflowExecutionOptions? options = null)
    {
        Options = options ?? new WorkflowExecutionOptions();
        RunId = Options.RunId ?? Guid.NewGuid();
        StartedAt = DateTimeOffset.UtcNow;
    }

    public Guid RunId { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? PlanFingerprint { get; private set; }
    public WorkflowExecutionOptions Options { get; }
    public IReadOnlyList<WorkflowEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    /// <summary>External inputs supplied by a host trigger for this run.</summary>
    public IReadOnlyDictionary<string, object?> Inputs
    {
        get
        {
            lock (_gate)
                return new Dictionary<string, object?>(_inputs, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SetInput(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate) _inputs[name.Trim()] = value;
    }

    internal void Record(WorkflowEvent item)
    {
        lock (_gate)
        {
            _events.Add(item);
        }
    }

    internal void BindPlan(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new ArgumentException("A plan fingerprint is required.", nameof(fingerprint));
        }
        if (PlanFingerprint is not null &&
            !PlanFingerprint.Equals(fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A workflow run cannot be rebound to another plan.");
        }
        PlanFingerprint = fingerprint;
    }

    internal void Complete() => CompletedAt = DateTimeOffset.UtcNow;
}

public interface IWorkflowStateStore
{
    ValueTask AppendAsync(WorkflowEvent item, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<WorkflowEvent>> ReadAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

/// <summary>Serializes typed command results that must survive a workflow restart.</summary>
public interface IWorkflowValueSerializer
{
    string? Serialize(object? value, Type declaredType);
    object? Deserialize(string? json, Type declaredType);
}

/// <summary>Default JSON serializer for built-in and ordinary DTO result types.</summary>
public sealed class JsonWorkflowValueSerializer : IWorkflowValueSerializer
{
    public string? Serialize(object? value, Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        return value switch
        {
            null => null,
            FileInfo file => JsonSerializer.Serialize(file.FullName),
            DirectoryInfo directory => JsonSerializer.Serialize(directory.FullName),
            Uri uri => JsonSerializer.Serialize(uri.AbsoluteUri),
            _ => JsonSerializer.Serialize(value, value.GetType())
        };
    }

    public object? Deserialize(string? json, Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        if (json is null)
        {
            return null;
        }
        if (declaredType == typeof(FileInfo))
        {
            return new FileInfo(JsonSerializer.Deserialize<string>(json)!);
        }
        if (declaredType == typeof(DirectoryInfo))
        {
            return new DirectoryInfo(JsonSerializer.Deserialize<string>(json)!);
        }
        if (declaredType == typeof(Uri))
        {
            return new Uri(JsonSerializer.Deserialize<string>(json)!, UriKind.Absolute);
        }
        return JsonSerializer.Deserialize(json, declaredType);
    }
}

/// <summary>Thread-safe default journal. Replace it to resume across processes.</summary>
public sealed class InMemoryWorkflowStateStore : IWorkflowStateStore
{
    private readonly ConcurrentDictionary<Guid, List<WorkflowEvent>> _runs = [];

    public ValueTask AppendAsync(
        WorkflowEvent item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<WorkflowEvent> events = _runs.GetOrAdd(item.RunId, _ => []);
        lock (events)
        {
            events.Add(item);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<WorkflowEvent>> ReadAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_runs.TryGetValue(runId, out List<WorkflowEvent>? events))
        {
            return ValueTask.FromResult<IReadOnlyList<WorkflowEvent>>(Array.Empty<WorkflowEvent>());
        }
        lock (events)
        {
            return ValueTask.FromResult<IReadOnlyList<WorkflowEvent>>(events.ToArray());
        }
    }
}

/// <summary>Append-only JSON-lines journal suitable for single-host durable runs.</summary>
public sealed class JsonFileWorkflowStateStore : IWorkflowStateStore
{
    private readonly string _directory;
    private readonly IExecutionPolicy _policy;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = [];

    public JsonFileWorkflowStateStore(string directory, IExecutionPolicy policy)
    {
        _directory = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask AppendAsync(
        WorkflowEvent item,
        CancellationToken cancellationToken = default)
    {
        string path = PathFor(item.RunId);
        _policy.EnsureFileAccess(path);
        Directory.CreateDirectory(_directory);
        SemaphoreSlim gate = _locks.GetOrAdd(item.RunId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string line = JsonSerializer.Serialize(item) + Environment.NewLine;
            await File.AppendAllTextAsync(path, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<WorkflowEvent>> ReadAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        string path = PathFor(runId);
        _policy.EnsureFileAccess(path);
        SemaphoreSlim gate = _locks.GetOrAdd(runId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return Array.Empty<WorkflowEvent>();
            }
            string[] lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
            return lines.Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<WorkflowEvent>(line)
                    ?? throw new InvalidDataException($"Invalid workflow journal entry in '{path}'."))
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    private string PathFor(Guid runId) => Path.Combine(_directory, $"{runId:N}.jsonl");
}
