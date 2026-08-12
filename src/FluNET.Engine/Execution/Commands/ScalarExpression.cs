using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Variables;
using System.Globalization;

namespace FluNET.Execution.Commands;

public interface IValueConverter<out TValue> : IValueCodec<TValue>
{
    TValue Convert(object value);

    TValue IValueCodec<TValue>.Decode(object value) => Convert(value);
}

/// <summary>A deferred scalar conversion from one semantically bound token.</summary>
public sealed class ScalarExpression<TValue> : IValueExpression<TValue>
{
    private readonly PromptToken _token;
    private readonly IValueCodec<TValue> _codec;

    public ScalarExpression(BoundArgument argument, IValueCodec<TValue> codec)
    {
        ArgumentNullException.ThrowIfNull(argument);
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _token = argument.Tokens.Count == 1
            ? argument.Tokens[0]
            : throw new ArgumentException(
                $"Semantic role {argument.Role} must contain exactly one scalar token.",
                nameof(argument));
    }

    public TValue Evaluate(IExpressionEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        object value = _token.Kind == PromptTokenKind.Variable
            ? ResolveVariable(context.Variables, _token.Text)
            : Unwrap(_token);
        return value is TValue typed ? typed : _codec.Decode(value);
    }

    public TValue Evaluate(IVariableResolver variables) =>
        Evaluate(new ExpressionEvaluationContext(variables));

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

public sealed class DecimalValueConverter : IValueConverter<decimal>
{
    public decimal Convert(object value) => value is decimal number
        ? number
        : decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : throw new FormatException($"'{value}' is not a number.");
}

public sealed class BooleanValueConverter : IValueConverter<bool>
{
    public bool Convert(object value) => value switch
    {
        bool boolean => boolean,
        _ when bool.TryParse(value.ToString(), out bool parsed) => parsed,
        _ when value.ToString()?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true => true,
        _ when value.ToString()?.Equals("no", StringComparison.OrdinalIgnoreCase) == true => false,
        _ => throw new FormatException($"'{value}' is not a boolean.")
    };
}
