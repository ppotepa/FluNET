using FluNET.Prompt.Expressions;
using FluNET.Variables;
using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed class JsonDataExpression
{
    private readonly ExpressionSyntax _syntax;
    private JsonDataExpression(ExpressionSyntax syntax) => _syntax = syntax;

    public static JsonDataExpression Parse(string source) => new(ExpressionSyntaxParser.Parse(source));
    public object? Evaluate(JsonElement row, IVariableResolver variables) => EvaluateNode(_syntax, row, variables);
    public bool EvaluateBoolean(JsonElement row, IVariableResolver variables) => ToBoolean(Evaluate(row, variables));

    public static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return 1;
        if (right is null) return -1;
        if (TryDecimal(left, out decimal a) && TryDecimal(right, out decimal b)) return a.CompareTo(b);
        if (left is bool lb && right is bool rb) return lb.CompareTo(rb);
        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static object? EvaluateNode(ExpressionSyntax syntax, JsonElement row, IVariableResolver variables) => syntax switch
    {
        LiteralExpressionSyntax literal => Literal(literal.Text, row),
        VariableExpressionSyntax variable => variables.Resolve<object>($"[{variable.Name}]")
            ?? throw new InvalidOperationException($"Variable [{variable.Name}] was not found for data expression."),
        ParenthesizedExpressionSyntax grouped => EvaluateNode(grouped.Expression, row, variables),
        PropertyExpressionSyntax property => Property(EvaluateNode(property.Target, row, variables), property.Property),
        IndexExpressionSyntax index => Index(EvaluateNode(index.Target, row, variables), EvaluateNode(index.Index, row, variables)),
        UnaryExpressionSyntax unary => Unary(unary, row, variables),
        BinaryExpressionSyntax binary => Binary(binary, row, variables),
        ListExpressionSyntax list => list.Items.Select(item => EvaluateNode(item, row, variables)).ToArray(),
        ObjectExpressionSyntax value => value.Fields.ToDictionary(field => field.Name, field => EvaluateNode(field.Value, row, variables), StringComparer.OrdinalIgnoreCase),
        _ => throw new NotSupportedException($"Unsupported data expression node '{syntax.GetType().Name}'.")
    };

    private static object? Literal(string text, JsonElement row)
    {
        string value = text.Trim();
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) return value[1..^1];
        if (bool.TryParse(value, out bool boolean)) return boolean;
        if (DataUnits.TryParse(value, out decimal sized)) return sized;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)) return number;
        return IsIdentifier(value) ? Property(row, value) : value;
    }

    private static object? Unary(UnaryExpressionSyntax unary, JsonElement row, IVariableResolver variables)
    {
        object? value = EvaluateNode(unary.Operand, row, variables);
        return unary.Operator.ToUpperInvariant() switch
        {
            "NOT" or "!" => !ToBoolean(value),
            "-" when TryDecimal(value, out decimal number) => -number,
            _ => throw new InvalidOperationException($"Operator '{unary.Operator}' is not valid for value '{value}'.")
        };
    }

    private static object? Binary(BinaryExpressionSyntax binary, JsonElement row, IVariableResolver variables)
    {
        object? left = EvaluateNode(binary.Left, row, variables);
        string op = binary.Operator.ToUpperInvariant();
        if (op == "??") return left ?? EvaluateNode(binary.Right, row, variables);
        if (op == "AND" && !ToBoolean(left)) return false;
        if (op == "OR" && ToBoolean(left)) return true;
        object? right = EvaluateNode(binary.Right, row, variables);
        return op switch
        {
            "AND" => ToBoolean(left) && ToBoolean(right),
            "OR" => ToBoolean(left) || ToBoolean(right),
            "==" => Equal(left, right),
            "!=" => !Equal(left, right),
            "CONTAINS" => Text(left).Contains(Text(right), StringComparison.OrdinalIgnoreCase),
            "MATCHES" => GlobMatch(Text(left), Text(right)),
            "STARTS WITH" => Text(left).StartsWith(Text(right), StringComparison.OrdinalIgnoreCase),
            "ENDS WITH" => Text(left).EndsWith(Text(right), StringComparison.OrdinalIgnoreCase),
            "<" => CompareValues(left, right) < 0,
            "<=" => CompareValues(left, right) <= 0,
            ">" => CompareValues(left, right) > 0,
            ">=" => CompareValues(left, right) >= 0,
            "+" => Arithmetic(left, right, (a, b) => a + b),
            "-" => Arithmetic(left, right, (a, b) => a - b),
            "*" => Arithmetic(left, right, (a, b) => a * b),
            "/" => Arithmetic(left, right, (a, b) => b == 0 ? throw new DivideByZeroException() : a / b),
            _ => throw new NotSupportedException($"Data expression operator '{binary.Operator}' is not supported.")
        };
    }

    private static object Arithmetic(object? left, object? right, Func<decimal, decimal, decimal> operation)
    {
        if (!TryDecimal(left, out decimal a) || !TryDecimal(right, out decimal b))
            throw new InvalidOperationException("Arithmetic data operators require Number operands.");
        return operation(a, b);
    }

    private static bool Equal(object? left, object? right)
    {
        if (left is null || right is null) return left is null && right is null;
        if (TryDecimal(left, out decimal a) && TryDecimal(right, out decimal b)) return a == b;
        return Equals(left, right) || string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string Text(object? value) => value?.ToString() ?? string.Empty;

    private static bool GlobMatch(string value, string pattern)
    {
        int valueIndex = 0;
        int patternIndex = 0;
        int wildcard = -1;
        int wildcardValue = -1;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                valueIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                wildcard = patternIndex++;
                wildcardValue = valueIndex;
            }
            else if (wildcard >= 0)
            {
                patternIndex = wildcard + 1;
                valueIndex = ++wildcardValue;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*') patternIndex++;
        return patternIndex == pattern.Length;
    }

    private static bool ToBoolean(object? value)
    {
        if (value is null) return false;
        if (value is bool boolean) return boolean;
        if (value is string text) return bool.TryParse(text, out bool parsed) ? parsed : !string.IsNullOrWhiteSpace(text);
        return TryDecimal(value, out decimal number) ? number != 0 : true;
    }

    private static bool TryDecimal(object? value, out decimal number)
    {
        if (value is decimal direct) { number = direct; return true; }
        if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double)
        {
            try { number = Convert.ToDecimal(value, CultureInfo.InvariantCulture); return true; }
            catch (Exception exception) when (exception is OverflowException or FormatException) { }
        }
        if (value is string text && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number)) return true;
        number = default;
        return false;
    }

    private static object? Property(object? source, string name)
    {
        if (source is null) return null;
        if (source is JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object) return null;
            if (json.TryGetProperty(name, out JsonElement exact)) return JsonValue(exact);
            foreach (JsonProperty property in json.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return JsonValue(property.Value);
            return null;
        }
        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                if (entry.Key?.ToString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true) return entry.Value;
        }
        return null;
    }

    private static object? Index(object? source, object? key)
    {
        if (source is null || key is null) return null;
        if (source is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Array && int.TryParse(key.ToString(), out int index) && index >= 0 && index < json.GetArrayLength()) return JsonValue(json[index]);
            if (json.ValueKind == JsonValueKind.Object) return Property(json, key.ToString() ?? string.Empty);
        }
        if (source is IList list && int.TryParse(key.ToString(), out int listIndex) && listIndex >= 0 && listIndex < list.Count) return list[listIndex];
        return null;
    }

    private static object? JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.Clone()
    };

    private static bool IsIdentifier(string value) => value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_') && value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
}
