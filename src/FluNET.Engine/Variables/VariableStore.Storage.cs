namespace FluNET.Variables;

public sealed partial class VariableStore
{
    private readonly Dictionary<string, RuntimeValue> _values = new(StringComparer.OrdinalIgnoreCase);
}
