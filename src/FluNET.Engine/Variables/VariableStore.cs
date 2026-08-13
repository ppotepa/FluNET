using FluNET.Language;

namespace FluNET.Variables;

public enum VariableScopeKind
{
    Host,
    Workflow,
    Block,
    Iteration
}

public sealed record RuntimeValue(TypeSymbol Type, object Value);

public interface IVariableStore
{
    bool TryGet(string name, out RuntimeValue? value);
    RuntimeValue Get(string name);
    void RegisterHost<T>(string name, T value);
    void Declare(VariableSymbol symbol, object value, VariableScopeKind scope = VariableScopeKind.Workflow);
    void Set(VariableSymbol symbol, object value, VariableScopeKind scope = VariableScopeKind.Workflow);
    IReadOnlyDictionary<string, RuntimeValue> Snapshot();
    void Clear(VariableScopeKind? scope = null);
}
