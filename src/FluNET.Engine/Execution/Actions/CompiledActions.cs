using FluNET.Capabilities;
using FluNET.Execution.Commands;
using FluNET.Variables;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FluNET.Execution.Actions;

/// <summary>Pre-bound nested operation executed against an explicit action scope.</summary>
public interface ICompiledAction
{
    string Kind { get; }
    ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default);
}

public sealed record CompiledActionTemplate(IReadOnlyList<ICompiledAction> Actions)
{
    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default)
    {
        foreach (ICompiledAction action in Actions)
            await action.ExecuteAsync(variables, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class CompiledSayAction(IExpression<string> expression, ITextOutput output) : ICompiledAction
{
    public string Kind => "SAY";
    public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken = default) =>
        await output.WriteLineAsync(expression.Evaluate(variables), cancellationToken).ConfigureAwait(false);
}

/// <summary>Iteration/task-local variables shadow, but never mutate, the parent resolver.</summary>
public sealed class ActionScopeVariableResolver : IVariableResolver
{
    private readonly IVariableResolver _parent;
    private readonly ConcurrentDictionary<string, object?> _local = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _readOnly;

    public ActionScopeVariableResolver(IVariableResolver parent, IEnumerable<KeyValuePair<string, object?>>? initial = null, IEnumerable<string>? readOnly = null)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _readOnly = new HashSet<string>((readOnly ?? []).Select(Normalize), StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> item in initial ?? []) _local[Normalize(item.Key)] = item.Value;
    }

    public void Register<T>(string name, T value)
    {
        string key = Normalize(name);
        if (_readOnly.Contains(key)) throw new InvalidOperationException($"Action variable '{key}' is read-only.");
        _local[key] = value;
    }

    public bool IsRegistered(string name) => _local.ContainsKey(Normalize(name)) || _parent.IsRegistered(name);

    public T? Resolve<T>(string tokenValue)
    {
        string key = Normalize(tokenValue);
        if (_local.TryGetValue(key, out object? value))
        {
            if (value is null) return default;
            if (value is T typed) return typed;
            if (typeof(T) == typeof(object)) return (T)value;
            if (value is JsonElement json && typeof(T) == typeof(JsonElement)) return (T)(object)json;
            return default;
        }
        return _parent.Resolve<T>(tokenValue);
    }

    public void Clear() => _local.Clear();
    public IEnumerable<string> GetVariableNames() => _parent.GetVariableNames().Concat(_local.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
    private static string Normalize(string value) => value.Trim().TrimStart('[').TrimEnd(']');
}
