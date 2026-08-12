using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public interface IValueConverter<out TValue>
{
    TValue Convert(object value);
}

/// <summary>A deferred scalar conversion from one semantically bound token.</summary>
public sealed class ScalarExpression<TValue> : IValueExpression<TValue>
{
    private readonly PromptToken _token;
    private readonly IValueConverter<TValue> _converter;

    public ScalarExpression(BoundArgument argument, IValueConverter<TValue> converter)
    {
        ArgumentNullException.ThrowIfNull(argument);
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _token = argument.Tokens.Count == 1
            ? argument.Tokens[0]
            : throw new ArgumentException(
                $"Semantic role {argument.Role} must contain exactly one scalar token.",
                nameof(argument));
    }

    public TValue Evaluate(IVariableResolver variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        object value = _token.Kind == PromptTokenKind.Variable
            ? ResolveVariable(variables, _token.Text)
            : Unwrap(_token);
        return value is TValue typed ? typed : _converter.Convert(value);
    }

    private static object ResolveVariable(IVariableResolver variables, string reference) =>
        variables.Resolve<object>(reference.TrimEnd('.'))
        ?? throw new InvalidOperationException($"Variable {reference.TrimEnd('.')} not found in context.");

    private static string Unwrap(PromptToken token)
    {
        string value = token.Text;
        if (token.Kind == PromptTokenKind.Reference && value.Length >= 2)
        {
            return value[1..^1];
        }

        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}

public sealed class StringValueConverter : IValueConverter<string>
{
    public string Convert(object value) => value.ToString() ?? string.Empty;
}

public sealed class FileInfoValueConverter : IValueConverter<FileInfo>
{
    public FileInfo Convert(object value) => new(value.ToString()
        ?? throw new InvalidCastException("A file path cannot be null."));
}

public sealed class DirectoryInfoValueConverter : IValueConverter<DirectoryInfo>
{
    public DirectoryInfo Convert(object value) => new(value.ToString()
        ?? throw new InvalidCastException("A directory path cannot be null."));
}

public sealed class UriValueConverter : IValueConverter<Uri>
{
    public Uri Convert(object value) => Uri.TryCreate(value.ToString(), UriKind.Absolute, out Uri? uri)
        ? uri
        : throw new FormatException($"'{value}' is not an absolute URI.");
}

public sealed class EncodingValueConverter : IValueConverter<System.Text.Encoding>
{
    public System.Text.Encoding Convert(object value) => value.ToString()?.ToUpperInvariant() switch
    {
        "UTF8" or "UTF-8" => System.Text.Encoding.UTF8,
        "UTF32" or "UTF-32" => System.Text.Encoding.UTF32,
        "ASCII" => System.Text.Encoding.ASCII,
        "UNICODE" => System.Text.Encoding.Unicode,
        string name => System.Text.Encoding.GetEncoding(name),
        _ => throw new InvalidCastException("An encoding name cannot be null.")
    };
}
