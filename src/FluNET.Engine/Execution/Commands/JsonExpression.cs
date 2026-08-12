using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

/// <summary>A deferred JSON value that accepts literals, text, or stored JSON.</summary>
public sealed class JsonExpression : IExpression<JsonElement>
{
    private readonly PromptToken _token;

    public JsonExpression(BoundArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        _token = argument.Tokens.Count == 1
            ? argument.Tokens[0]
            : throw new ArgumentException(
                $"Semantic role {argument.RoleId} must contain one JSON value.",
                nameof(argument));
    }

    public JsonElement Evaluate(IExpressionEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        object value = _token.Kind == PromptTokenKind.Variable
            ? (context.Variables.Resolve<object>(_token.Text.TrimEnd('.'))
                ?? throw new InvalidOperationException($"Variable {_token.Text.TrimEnd('.')} not found in context."))
            : SurfaceValue(_token);
        return new JsonValueCodec().Decode(value);
    }

    private static string SurfaceValue(PromptToken token)
    {
        string value = token.Text;
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }
        return value;
    }
}

public sealed class JsonValueCodec : IValueCodec<JsonElement>
{
    public JsonElement Decode(object value)
    {
        if (value is JsonElement element)
        {
            return element.Clone();
        }

        string json = value switch
        {
            string text => text,
            string[] lines => string.Join('\n', lines),
            _ => JsonSerializer.Serialize(value)
        };
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
