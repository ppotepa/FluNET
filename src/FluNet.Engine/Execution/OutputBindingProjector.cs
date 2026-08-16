using FluNET.Binding;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Core;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FluNET.Execution;

/// <summary>
/// Projects a single CLR result into one or many output WHAT bindings. Named slots prefer
/// dictionary/property metadata; positional tuples/lists are used as a fallback.
/// </summary>
public sealed class OutputBindingProjector
{
    public IReadOnlyDictionary<string, object?> Project(BoundSentence sentence, object? result)
    {
        var bindings = sentence.Roles
            .Where(x => x.Descriptor.Direction is RoleDirection.Output or RoleDirection.InputOutput)
            .SelectMany(role => role.Values
                .Where(value => value.Source is VariableExpression)
                .Select(value => new OutputSlot(
                    ((VariableExpression)value.Source).Name,
                    role.Descriptor.Name)))
            .ToArray();

        if (bindings.Length == 0)
            return new Dictionary<string, object?>();

        if (bindings.Length == 1)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [bindings[0].VariableName] = result
            };

        var projected = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (TryProjectDictionary(result, bindings, projected)) return projected;
        if (TryProjectProperties(result, bindings, projected)) return projected;
        if (TryProjectTuple(result, bindings, projected)) return projected;
        if (TryProjectList(result, bindings, projected)) return projected;

        throw new InvalidOperationException(
            $"Verb '{sentence.Verb.Text}' returned '{result?.GetType().FullName ?? "null"}' for {bindings.Length} output bindings, but the result cannot be projected by name or position.");
    }

    private static bool TryProjectDictionary(object? result, OutputSlot[] slots, IDictionary<string, object?> output)
    {
        if (result is IReadOnlyDictionary<string, object?> readOnly)
        {
            foreach (OutputSlot slot in slots)
            {
                string key = slot.SlotName ?? slot.VariableName;
                KeyValuePair<string, object?> match = readOnly.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (match.Key == null) { output.Clear(); return false; }
                output[slot.VariableName] = match.Value;
            }
            return true;
        }

        if (result is IDictionary dictionary)
        {
            foreach (OutputSlot slot in slots)
            {
                string key = slot.SlotName ?? slot.VariableName;
                object? foundKey = dictionary.Keys.Cast<object?>().FirstOrDefault(x => x?.ToString()?.Equals(key, StringComparison.OrdinalIgnoreCase) == true);
                if (foundKey == null) { output.Clear(); return false; }
                output[slot.VariableName] = dictionary[foundKey];
            }
            return true;
        }

        return false;
    }

    private static bool TryProjectProperties(object? result, OutputSlot[] slots, IDictionary<string, object?> output)
    {
        if (result == null) return false;
        PropertyInfo[] properties = result.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (OutputSlot slot in slots)
        {
            string name = slot.SlotName ?? slot.VariableName;
            PropertyInfo? property = properties.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (property == null) { output.Clear(); return false; }
            output[slot.VariableName] = property.GetValue(result);
        }
        return true;
    }

    private static bool TryProjectTuple(object? result, OutputSlot[] slots, IDictionary<string, object?> output)
    {
        if (result is not ITuple tuple || tuple.Length != slots.Length) return false;
        for (int i = 0; i < slots.Length; i++) output[slots[i].VariableName] = tuple[i];
        return true;
    }

    private static bool TryProjectList(object? result, OutputSlot[] slots, IDictionary<string, object?> output)
    {
        if (result is not IList list || list.Count != slots.Length) return false;
        for (int i = 0; i < slots.Length; i++) output[slots[i].VariableName] = list[i];
        return true;
    }

    private sealed record OutputSlot(string VariableName, string? SlotName);
}
