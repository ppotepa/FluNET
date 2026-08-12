using FluNET.Variables;
using System.Collections.ObjectModel;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Execution.Commands;

/// <summary>A deferred, typed value evaluated against the execution scope.</summary>
public interface IValueExpression<out TValue>
{
    TValue Evaluate(IVariableResolver variables);
}

public abstract record TextPart;

public sealed record LiteralTextPart(string Value) : TextPart;

public sealed record VariableTextPart(string Reference) : TextPart;

/// <summary>An immutable text expression composed from literals and variables.</summary>
public sealed class TextExpression : IValueExpression<string>
{
    private readonly ReadOnlyCollection<TextPart> _parts;

    public TextExpression(IEnumerable<TextPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        _parts = Array.AsReadOnly(parts.ToArray());
    }

    public IReadOnlyList<TextPart> Parts => _parts;

    public static TextExpression Bind(
        BoundArgument argument,
        LanguageSnapshot language,
        bool preserveStructuredReferences = false)
    {
        ArgumentNullException.ThrowIfNull(argument);
        ArgumentNullException.ThrowIfNull(language);
        return new TextExpression(argument.Tokens.Select(token =>
            BindPart(token, language, preserveStructuredReferences)));
    }

    public string Evaluate(IVariableResolver variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        List<string> values = [];

        foreach (TextPart part in _parts)
        {
            object value = part switch
            {
                LiteralTextPart literal => literal.Value,
                VariableTextPart variable => ResolveVariable(variables, variable.Reference),
                _ => throw new InvalidOperationException($"Unsupported text part '{part.GetType().FullName}'.")
            };

            AppendValue(values, value);
        }

        return string.Join(" ", values);
    }

    private static object ResolveVariable(IVariableResolver variables, string reference)
    {
        string normalized = reference.TrimEnd('.');
        return variables.Resolve<object>(normalized)
            ?? throw new InvalidOperationException(
                $"Variable {normalized} not found in context. " +
                $"Variables must be stored before use with commands like: GET {normalized} FROM file.txt");
    }

    private static TextPart BindPart(
        PromptToken token,
        LanguageSnapshot language,
        bool preserveStructuredReferences) => token.Kind switch
    {
        PromptTokenKind.Variable => new VariableTextPart(token.Text),
        PromptTokenKind.Reference when preserveStructuredReferences && LooksLikeJson(token.Text) =>
            new LiteralTextPart(token.Text),
        PromptTokenKind.Reference => new LiteralTextPart(Unwrap(token.Text, '{', '}')),
        _ => new LiteralTextPart(NormalizeLiteral(token.Text, language))
    };

    private static bool LooksLikeJson(string value) =>
        value.Length >= 2 && value[0] == '{' && value[^1] == '}' && value.Contains(':');

    private static string NormalizeLiteral(string value, LanguageSnapshot language)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return Unwrap(value, value[0], value[^1])
                .Replace("\\\"", "\"")
                .Replace("\\'", "'")
                .Replace("\\\\", "\\");
        }

        return language.FindCommand(value)?.Name ?? value;
    }

    private static string Unwrap(string value, char opening, char closing) =>
        value.Length >= 2 && value[0] == opening && value[^1] == closing
            ? value[1..^1]
            : value;

    private static void AppendValue(ICollection<string> values, object value)
    {
        if (value is string[] lines)
        {
            foreach (string line in lines.Where(line => !string.IsNullOrEmpty(line)))
            {
                values.Add(line.TrimEnd('.'));
            }
            return;
        }

        string? text = value.ToString();
        if (!string.IsNullOrEmpty(text))
        {
            values.Add(text.TrimEnd('.'));
        }
    }
}
