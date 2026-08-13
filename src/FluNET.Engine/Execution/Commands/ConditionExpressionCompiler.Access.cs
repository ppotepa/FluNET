using FluNET.Prompt.Expressions;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed partial class ConditionExpressionCompiler
{
    private static Func<IExpressionEvaluationContext, object?> CompileProperty(
        PropertyExpressionSyntax property,
        ISet<string> variables)
    {
        Func<IExpressionEvaluationContext, object?> target =
            CompileNode(property.Target, variables);
        return context => ReadProperty(target(context), property.Property);
    }

    private static Func<IExpressionEvaluationContext, object?> CompileIndex(
        IndexExpressionSyntax index,
        ISet<string> variables)
    {
        Func<IExpressionEvaluationContext, object?> target =
            CompileNode(index.Target, variables);
        Func<IExpressionEvaluationContext, object?> key =
            CompileNode(index.Index, variables);
        return context => ReadIndex(target(context), key(context));
    }

    private static Func<IExpressionEvaluationContext, object?> CompileList(
        ListExpressionSyntax list,
        ISet<string> variables)
    {
        Func<IExpressionEvaluationContext, object?>[] items = list.Items
            .Select(item => CompileNode(item, variables))
            .ToArray();
        return context => items.Select(item => item(context)).ToArray();
    }

    private static Func<IExpressionEvaluationContext, object?> CompileObject(
        ObjectExpressionSyntax value,
        ISet<string> variables)
    {
        (string Name, Func<IExpressionEvaluationContext, object?> Value)[] fields =
            value.Fields
                .Select(field => (field.Name, CompileNode(field.Value, variables)))
                .ToArray();
        return context => fields.ToDictionary(
            field => field.Name,
            field => field.Value(context),
            StringComparer.OrdinalIgnoreCase);
    }

    private static object? ReadProperty(object? target, string property)
    {
        if (target is null)
        {
            return null;
        }
        if (target is JsonElement json && json.ValueKind == JsonValueKind.Object &&
            json.TryGetProperty(property, out JsonElement jsonValue))
        {
            return JsonValue(jsonValue);
        }
        if (target is IReadOnlyDictionary<string, object?> readOnly &&
            readOnly.TryGetValue(property, out object? readOnlyValue))
        {
            return readOnlyValue;
        }
        if (target is IDictionary<string, object?> dictionary &&
            dictionary.TryGetValue(property, out object? dictionaryValue))
        {
            return dictionaryValue;
        }

        PropertyInfo? reflected = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate => candidate.Name.Equals(property, StringComparison.OrdinalIgnoreCase));
        return reflected?.GetValue(target);
    }

    private static object? ReadIndex(object? target, object? key)
    {
        if (target is null || key is null)
        {
            return null;
        }
        if (target is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array && TryIndex(key, out int jsonIndex))
            {
                return JsonValue(json[jsonIndex]);
            }
            if (json.ValueKind == JsonValueKind.Object &&
                json.TryGetProperty(key.ToString() ?? string.Empty, out JsonElement property))
            {
                return JsonValue(property);
            }
        }
        if (target is IList list && TryIndex(key, out int listIndex))
        {
            return list[listIndex];
        }
        if (target is Array array && TryIndex(key, out int arrayIndex))
        {
            return array.GetValue(arrayIndex);
        }
        if (target is IDictionary dictionary)
        {
            return dictionary[key];
        }
        return null;
    }

    private static bool TryIndex(object value, out int index) =>
        value is int direct
            ? (index = direct) >= 0
            : int.TryParse(value.ToString(), out index) && index >= 0;

    private static object? JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Null => null,
        _ => value.Clone()
    };
}
