using FluNET.Language;

namespace FluNET.Variables;

public sealed partial class VariableStore
{
    public bool TryGet(string name, out RuntimeValue? value)
    {
        string normalized = Normalize(name);
        lock (_gate)
        {
            foreach (VariableScopeKind scope in SearchOrder)
            {
                if (_values.TryGetValue(Key(scope, normalized), out RuntimeValue? found))
                {
                    value = found;
                    return true;
                }
            }
        }
        value = null;
        return false;
    }

    public RuntimeValue Get(string name) =>
        TryGet(name, out RuntimeValue? value)
            ? value!
            : throw new KeyNotFoundException($"Variable '{name}' is not defined.");

    public void RegisterHost<T>(string name, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        TypeSymbol type = _language.Types.Get(value.GetType());
        lock (_gate)
        {
            _values[Key(VariableScopeKind.Host, Normalize(name))] = new RuntimeValue(type, value);
        }
    }

    public void Declare(
        VariableSymbol symbol,
        object value,
        VariableScopeKind scope = VariableScopeKind.Workflow)
    {
        RuntimeValue runtime = Validate(symbol, value);
        string key = Key(scope, Normalize(symbol.Name));
        lock (_gate)
        {
            if (_values.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Variable '{symbol.Name}' is already declared in {scope} scope.");
            }
            _values[key] = runtime;
        }
    }

    public void Set(
        VariableSymbol symbol,
        object value,
        VariableScopeKind scope = VariableScopeKind.Workflow)
    {
        RuntimeValue runtime = Validate(symbol, value);
        lock (_gate)
        {
            if (TryGet(symbol.Name, out RuntimeValue? existing) && existing!.Type.Id != symbol.Type.Id)
            {
                throw new InvalidOperationException(
                    $"Variable '{symbol.Name}' cannot change type from '{existing.Type}' to '{symbol.Type}'.");
            }
            _values[Key(scope, Normalize(symbol.Name))] = runtime;
        }
    }

    public IReadOnlyDictionary<string, RuntimeValue> Snapshot()
    {
        Dictionary<string, RuntimeValue> result = new(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (VariableScopeKind scope in new[]
            {
                VariableScopeKind.Host,
                VariableScopeKind.Workflow,
                VariableScopeKind.Block,
                VariableScopeKind.Iteration
            })
            {
                string prefix = $"{(int)scope}:";
                foreach ((string key, RuntimeValue value) in _values.Where(item =>
                    item.Key.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    result[key[prefix.Length..]] = value;
                }
            }
        }
        return result;
    }

    public void Clear(VariableScopeKind? scope = null)
    {
        lock (_gate)
        {
            if (scope is null)
            {
                _values.Clear();
                return;
            }

            string prefix = $"{(int)scope.Value}:";
            foreach (string key in _values.Keys.Where(key =>
                key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                _values.Remove(key);
            }
        }
    }

    private RuntimeValue Validate(VariableSymbol symbol, object value)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(value);
        TypeSymbol actual = _language.Types.Get(value.GetType());
        bool assignable = symbol.Type.Id == actual.Id || symbol.Type.IsAssignableFrom(actual);
        if (!assignable)
        {
            throw new InvalidCastException(
                $"Variable '{symbol.Name}' expects '{symbol.Type}', received '{actual}'.");
        }
        return new RuntimeValue(symbol.Type, value);
    }

    private static readonly VariableScopeKind[] SearchOrder =
    [
        VariableScopeKind.Iteration,
        VariableScopeKind.Block,
        VariableScopeKind.Workflow,
        VariableScopeKind.Host
    ];

    private static string Key(VariableScopeKind scope, string name) => $"{(int)scope}:{name}";

    private static string Normalize(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A variable name is required.", nameof(name))
            : name.Trim().TrimStart('[').TrimEnd(']').ToUpperInvariant();
}
