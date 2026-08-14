using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Prompt;
using System.Text.Json;

namespace FluNET.Execution.Commands;

/// <summary>Turns semantically bound arguments into typed value expressions.</summary>
public sealed class ExpressionBinder
{
    private readonly LanguageSnapshot _language;
    private readonly IValueCodecRegistry _values;

    public ExpressionBinder(LanguageSnapshot language)
        : this(
            language ?? throw new ArgumentNullException(nameof(language)),
            ValueCodecRegistryFactory.CreateDefault(language))
    {
    }

    public ExpressionBinder(LanguageSnapshot language, IValueCodecRegistry values)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public IExpression<TValue> Bind<TValue>(BoundArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Tokens.Count != 1)
        {
            throw new ExpressionBindingException(
                ExpressionDiagnosticCodes.ShapeMismatch,
                $"Semantic role {argument.RoleId} must contain exactly one value for {_language.Types.Get<TValue>().Name}.",
                SpanOf(argument));
        }

        PromptToken token = argument.Tokens[0];
        if (token.Kind == PromptTokenKind.Variable)
        {
            return new VariableExpression<TValue>(
                token.Text,
                new RegistryExpressionCodec<TValue>(_language, _values));
        }

        return new LiteralExpression<TValue>(ParseLiteral<TValue>(token));
    }

    public IExpression<string> BindText(
        BoundArgument argument,
        bool preserveStructuredReferences = false)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Tokens.Count == 0)
        {
            return new LiteralExpression<string>(string.Empty);
        }

        IExpression<string>[] parts = argument.Tokens
            .Select(token => BindTextToken(token, preserveStructuredReferences))
            .ToArray();
        return parts.Length == 1
            ? parts[0]
            : new JoinedTextExpression(parts);
    }

    public IReadOnlyList<IExpression<TValue>> BindRepeated<TValue>(BoundArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return argument.Tokens.Select(token => BindSingle<TValue>(token)).ToArray();
    }

    private IExpression<TValue> BindSingle<TValue>(PromptToken token)
    {
        if (token.Kind == PromptTokenKind.Variable)
        {
            return new VariableExpression<TValue>(
                token.Text,
                new RegistryExpressionCodec<TValue>(_language, _values));
        }

        return new LiteralExpression<TValue>(ParseLiteral<TValue>(token));
    }

    private TValue ParseLiteral<TValue>(PromptToken token)
    {
        TypeSymbol target = _language.Types.Get<TValue>();
        try
        {
            object parsed = _values.Parse(target.Id, new ValueLiteral(token.Text));
            if (parsed is TValue typed)
            {
                return typed;
            }
            if (target.Id == BuiltInTypeIds.Number)
            {
                return NumericRuntimeAdapter.ConvertTo<TValue>(parsed);
            }
            throw new InvalidCastException(
                $"Codec for '{target.Id}' returned '{parsed.GetType()}', expected '{typeof(TValue)}'.");
        }
        catch (Exception exception) when (
            exception is FormatException or InvalidCastException or InvalidOperationException or JsonException)
        {
            throw new ExpressionBindingException(
                ExpressionDiagnosticCodes.ValueParseFailure,
                $"Cannot parse '{token.Text}' as '{target.Name}': {exception.Message}",
                token.Span,
                exception);
        }
    }

    private IExpression<string> BindTextToken(
        PromptToken token,
        bool preserveStructuredReferences)
    {
        if (token.Kind == PromptTokenKind.Variable)
        {
            return new VariableExpression<string>(
                token.Text,
                new RegistryExpressionCodec<string>(_language, _values));
        }

        if (token.Kind == PromptTokenKind.Reference &&
            preserveStructuredReferences &&
            LooksLikeJson(token.Text))
        {
            return new LiteralExpression<string>(token.Text);
        }

        string literal = token.Kind == PromptTokenKind.Reference
            ? UnwrapReference(token.Text)
            : NormalizeTextLiteral(token.Text);
        return new LiteralExpression<string>(literal);
    }

    private string NormalizeTextLiteral(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1]
                .Replace("\\\"", "\"")
                .Replace("\\'", "'")
                .Replace("\\\\", "\\");
        }
        return _language.FindCommand(value)?.Name ?? value;
    }

    private static bool LooksLikeJson(string value) =>
        value.Length >= 2 && value[0] == '{' && value[^1] == '}' && value.Contains(':');

    private static string UnwrapReference(string value) =>
        value.Length >= 2 && value[0] == '{' && value[^1] == '}'
            ? value[1..^1]
            : value;

    private static SourceSpan SpanOf(BoundArgument argument)
    {
        if (argument.Tokens.Count == 0)
        {
            return default;
        }
        PromptToken first = argument.Tokens[0];
        PromptToken last = argument.Tokens[^1];
        return SourceSpan.FromBounds(first.Span.Start, last.Span.End);
    }
}
