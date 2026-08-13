using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FluNET.Language.Values;

public readonly record struct ValueLiteral(string Text, object? Value = null)
{
    public object RawValue => Value ?? Text;
}

public sealed record ValueParseContext(LanguageSnapshot Language);
public sealed record ValueFormatContext(LanguageSnapshot Language);
public sealed record ValueConversionContext(LanguageSnapshot Language);

public interface IValueParser<out TValue>
{
    TValue Parse(ValueLiteral literal, ValueParseContext context);
}

public interface IValueFormatter<in TValue>
{
    string Format(TValue value, ValueFormatContext context);
}

public interface IValueCodec<TValue> : IValueParser<TValue>, IValueFormatter<TValue>
{
}

public interface IValueConversion<in TSource, out TTarget>
{
    TTarget Convert(TSource value, ValueConversionContext context);
}

public enum ConversionKind
{
    Implicit,
    Explicit
}

public sealed record ValueCodecDescriptor(TypeId TypeId, Type RuntimeType, Type CodecType);

public sealed record ValueConversionDescriptor(
    string Id,
    TypeId SourceType,
    TypeId TargetType,
    ConversionKind Kind,
    int Cost,
    Type SourceRuntimeType,
    Type TargetRuntimeType,
    Type ConversionType);

public sealed record ConversionPath
{
    public ConversionPath(IEnumerable<ValueConversionDescriptor> steps, int cost, bool isIdentity = false)
    {
        Steps = steps?.ToArray() ?? throw new ArgumentNullException(nameof(steps));
        Cost = cost;
        IsIdentity = isIdentity;
    }

    public IReadOnlyList<ValueConversionDescriptor> Steps { get; }
    public int Cost { get; }
    public bool IsIdentity { get; }

    public static ConversionPath Identity { get; } = new([], 0, true);
}

public sealed record ConversionResolution(ConversionPath? Path, bool IsAmbiguous)
{
    public bool IsFound => Path is not null && !IsAmbiguous;
}

public interface IValueCodecRegistry
{
    IReadOnlyCollection<ValueCodecDescriptor> Codecs { get; }
    IReadOnlyCollection<ValueConversionDescriptor> Conversions { get; }

    object Parse(TypeId typeId, ValueLiteral literal);
    TValue Parse<TValue>(ValueLiteral literal);
    string Format(TypeId typeId, object value);
    ConversionResolution ResolveConversion(
        TypeSymbol source,
        TypeSymbol target,
        bool allowExplicit = false);
    object Convert(object value, ConversionPath path);
    IReadOnlyList<ValueConversionDescriptor> FindConversions(TypeId sourceType);
}

internal interface IRuntimeValueCodec
{
    ValueCodecDescriptor Descriptor { get; }
    object Parse(ValueLiteral literal, ValueParseContext context);
    string Format(object value, ValueFormatContext context);
}

internal sealed class RuntimeValueCodec<TValue>(
    TypeId typeId,
    IValueCodec<TValue> codec) : IRuntimeValueCodec
{
    private readonly IValueCodec<TValue> _codec = codec ?? throw new ArgumentNullException(nameof(codec));

    public ValueCodecDescriptor Descriptor { get; } =
        new(typeId, typeof(TValue), codec?.GetType() ?? throw new ArgumentNullException(nameof(codec)));

    public object Parse(ValueLiteral literal, ValueParseContext context) =>
        _codec.Parse(literal, context)!;

    public string Format(object value, ValueFormatContext context) =>
        value is TValue typed
            ? _codec.Format(typed, context)
            : throw new InvalidCastException(
                $"Value '{value.GetType()}' cannot be formatted as '{typeof(TValue)}'.");
}

internal interface IRuntimeValueConversion
{
    ValueConversionDescriptor Descriptor { get; }
    object Convert(object value, ValueConversionContext context);
}

internal sealed class RuntimeValueConversion<TSource, TTarget>(
    ValueConversionDescriptor descriptor,
    IValueConversion<TSource, TTarget> conversion) : IRuntimeValueConversion
{
    private readonly IValueConversion<TSource, TTarget> _conversion =
        conversion ?? throw new ArgumentNullException(nameof(conversion));

    public ValueConversionDescriptor Descriptor { get; } = descriptor;

    public object Convert(object value, ValueConversionContext context) =>
        value is TSource typed
            ? _conversion.Convert(typed, context)!
            : throw new InvalidCastException(
                $"Conversion '{Descriptor.Id}' expected '{typeof(TSource)}', got '{value.GetType()}'.");
}

internal sealed record ValueCodecRegistration(
    Type ValueType,
    Type CodecType,
    Func<IServiceProvider, TypeId, IRuntimeValueCodec> Create);

internal sealed record ValueConversionRegistration(
    Type SourceType,
    Type TargetType,
    Type ConversionType,
    ConversionKind Kind,
    int Cost,
    Func<IServiceProvider, ValueConversionDescriptor, IRuntimeValueConversion> Create);

public sealed class ValueCodecRegistry : IValueCodecRegistry
{
    private readonly LanguageSnapshot _language;
    private readonly Dictionary<TypeId, IRuntimeValueCodec> _codecs = [];
    private readonly Dictionary<string, IRuntimeValueConversion> _conversions =
        new(StringComparer.Ordinal);

    internal ValueCodecRegistry(
        LanguageSnapshot language,
        IServiceProvider services,
        IEnumerable<ValueCodecRegistration> codecRegistrations,
        IEnumerable<ValueConversionRegistration> conversionRegistrations)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        ArgumentNullException.ThrowIfNull(services);
        RegisterBuiltIns();

        foreach (ValueCodecRegistration registration in codecRegistrations)
        {
            TypeSymbol symbol = _language.Types.Get(registration.ValueType);
            AddCodec(registration.Create(services, symbol.Id));
        }

        foreach (ValueConversionRegistration registration in conversionRegistrations)
        {
            TypeSymbol source = _language.Types.Get(registration.SourceType);
            TypeSymbol target = _language.Types.Get(registration.TargetType);
            ValueConversionDescriptor descriptor = CreateDescriptor(
                source,
                target,
                registration.SourceType,
                registration.TargetType,
                registration.ConversionType,
                registration.Kind,
                registration.Cost);
            AddConversion(registration.Create(services, descriptor));
        }
    }

    public IReadOnlyCollection<ValueCodecDescriptor> Codecs =>
        _codecs.Values.Select(codec => codec.Descriptor)
            .OrderBy(codec => codec.TypeId.Value, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyCollection<ValueConversionDescriptor> Conversions =>
        _conversions.Values.Select(conversion => conversion.Descriptor)
            .OrderBy(conversion => conversion.Id, StringComparer.Ordinal)
            .ToArray();

    public object Parse(TypeId typeId, ValueLiteral literal)
    {
        if (!_codecs.TryGetValue(typeId, out IRuntimeValueCodec? codec))
        {
            throw new InvalidOperationException($"No value codec is registered for language type '{typeId}'.");
        }
        return codec.Parse(literal, new ValueParseContext(_language));
    }

    public TValue Parse<TValue>(ValueLiteral literal)
    {
        TypeSymbol type = _language.Types.Get<TValue>();
        object parsed = Parse(type.Id, literal);
        return parsed is TValue value
            ? value
            : throw new InvalidCastException(
                $"Codec for '{type.Id}' returned '{parsed.GetType()}', expected '{typeof(TValue)}'.");
    }

    public string Format(TypeId typeId, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!_codecs.TryGetValue(typeId, out IRuntimeValueCodec? codec))
        {
            throw new InvalidOperationException($"No value codec is registered for language type '{typeId}'.");
        }
        return codec.Format(value, new ValueFormatContext(_language));
    }

    public IReadOnlyList<ValueConversionDescriptor> FindConversions(TypeId sourceType) =>
        _conversions.Values
            .Select(conversion => conversion.Descriptor)
            .Where(descriptor => descriptor.SourceType == sourceType)
            .OrderBy(descriptor => descriptor.Cost)
            .ThenBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToArray();

    public ConversionResolution ResolveConversion(
        TypeSymbol source,
        TypeSymbol target,
        bool allowExplicit = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (target.IsAssignableFrom(source))
        {
            return new ConversionResolution(ConversionPath.Identity, false);
        }

        List<PathCandidate> pending = [new(source.Id, 0, [], [source.Id])];
        int? bestTargetCost = null;
        List<ConversionPath> matches = [];
        Dictionary<TypeId, int> bestSeen = new() { [source.Id] = 0 };

        while (pending.Count > 0)
        {
            pending.Sort(PathCandidateComparer.Instance);
            PathCandidate current = pending[0];
            pending.RemoveAt(0);
            if (bestTargetCost is not null && current.Cost > bestTargetCost.Value)
            {
                break;
            }

            foreach (ValueConversionDescriptor edge in FindConversions(current.Type))
            {
                if (!allowExplicit && edge.Kind == ConversionKind.Explicit)
                {
                    continue;
                }
                if (current.Visited.Contains(edge.TargetType))
                {
                    continue;
                }

                int cost = checked(current.Cost + edge.Cost);
                if (bestTargetCost is not null && cost > bestTargetCost.Value)
                {
                    continue;
                }

                List<ValueConversionDescriptor> steps = [.. current.Steps, edge];
                TypeSymbol? reached = _language.Types.Find(edge.TargetType);
                if (reached is not null && target.IsAssignableFrom(reached))
                {
                    bestTargetCost ??= cost;
                    if (cost == bestTargetCost.Value)
                    {
                        matches.Add(new ConversionPath(steps, cost));
                    }
                    continue;
                }

                if (bestSeen.TryGetValue(edge.TargetType, out int previous) && cost > previous)
                {
                    continue;
                }
                bestSeen[edge.TargetType] = cost;
                pending.Add(new PathCandidate(
                    edge.TargetType,
                    cost,
                    steps,
                    [.. current.Visited, edge.TargetType]));
            }
        }

        ConversionPath[] distinct = matches
            .DistinctBy(path => string.Join("->", path.Steps.Select(step => step.Id)))
            .ToArray();
        return distinct.Length switch
        {
            0 => new ConversionResolution(null, false),
            1 => new ConversionResolution(distinct[0], false),
            _ => new ConversionResolution(null, true)
        };
    }

    public object Convert(object value, ConversionPath path)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(path);
        if (path.IsIdentity)
        {
            return value;
        }

        object current = value;
        ValueConversionContext context = new(_language);
        foreach (ValueConversionDescriptor descriptor in path.Steps)
        {
            if (!_conversions.TryGetValue(descriptor.Id, out IRuntimeValueConversion? conversion))
            {
                throw new InvalidOperationException($"Conversion '{descriptor.Id}' is not registered.");
            }
            current = conversion.Convert(current, context);
        }
        return current;
    }

    private void RegisterBuiltIns()
    {
        AddBuiltInCodec(_language.Types.Text, new TextValueCodec());
        AddBuiltInCodec(_language.Types.Boolean, new BooleanValueCodec());
        AddBuiltInCodec(_language.Types.Number, new NumberValueCodec());
        AddBuiltInCodec(_language.Types.File, new FileValueCodec());
        AddBuiltInCodec(_language.Types.Directory, new DirectoryValueCodec());
        AddBuiltInCodec(_language.Types.Uri, new UriValueCodec());
        AddBuiltInCodec(_language.Types.Json, new JsonElementValueCodec());
        TypeSymbol encoding = _language.Types.Get(typeof(Encoding));
        AddBuiltInCodec(encoding, new EncodingValueCodec());

        AddBuiltInConversion(_language.Types.Text, _language.Types.File,
            new TextToFileConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Text, _language.Types.Directory,
            new TextToDirectoryConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Text, _language.Types.Uri,
            new TextToUriConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Text, _language.Types.Number,
            new TextToNumberConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Text, _language.Types.Boolean,
            new TextToBooleanConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Text, _language.Types.Json,
            new TextToJsonConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Text, encoding,
            new TextToEncodingConversion(), ConversionKind.Implicit);

        AddBuiltInConversion(_language.Types.Number, _language.Types.Text,
            new NumberToTextConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Boolean, _language.Types.Text,
            new BooleanToTextConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.File, _language.Types.Text,
            new FileToTextConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Directory, _language.Types.Text,
            new DirectoryToTextConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Uri, _language.Types.Text,
            new UriToTextConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(_language.Types.Json, _language.Types.Text,
            new JsonToTextConversion(), ConversionKind.Implicit);
        AddBuiltInConversion(encoding, _language.Types.Text,
            new EncodingToTextConversion(), ConversionKind.Implicit);
    }

    private void AddBuiltInCodec<TValue>(TypeSymbol type, IValueCodec<TValue> codec) =>
        AddCodec(new RuntimeValueCodec<TValue>(type.Id, codec));

    private void AddBuiltInConversion<TSource, TTarget>(
        TypeSymbol source,
        TypeSymbol target,
        IValueConversion<TSource, TTarget> conversion,
        ConversionKind kind,
        int cost = 1)
    {
        ValueConversionDescriptor descriptor = CreateDescriptor(
            source,
            target,
            typeof(TSource),
            typeof(TTarget),
            conversion.GetType(),
            kind,
            cost);
        AddConversion(new RuntimeValueConversion<TSource, TTarget>(descriptor, conversion));
    }

    private void AddCodec(IRuntimeValueCodec codec)
    {
        if (!_codecs.TryAdd(codec.Descriptor.TypeId, codec))
        {
            throw new LanguageDefinitionException(
                $"A value codec for '{codec.Descriptor.TypeId}' is registered more than once.");
        }
    }

    private void AddConversion(IRuntimeValueConversion conversion)
    {
        if (conversion.Descriptor.Cost <= 0)
        {
            throw new LanguageDefinitionException("Conversion cost must be positive.");
        }
        if (conversion.Descriptor.SourceType == conversion.Descriptor.TargetType)
        {
            throw new LanguageDefinitionException(
                $"Conversion '{conversion.Descriptor.Id}' cannot convert a type to itself.");
        }
        if (!_conversions.TryAdd(conversion.Descriptor.Id, conversion))
        {
            throw new LanguageDefinitionException(
                $"Conversion id '{conversion.Descriptor.Id}' is registered more than once.");
        }
    }

    private static ValueConversionDescriptor CreateDescriptor(
        TypeSymbol source,
        TypeSymbol target,
        Type sourceRuntime,
        Type targetRuntime,
        Type implementation,
        ConversionKind kind,
        int cost)
    {
        if (cost <= 0)
        {
            throw new LanguageDefinitionException("Conversion cost must be positive.");
        }
        string implementationName = implementation.FullName ?? implementation.Name;
        string id = $"{source.Id.Value}->{target.Id.Value}:{implementationName}";
        return new ValueConversionDescriptor(
            id,
            source.Id,
            target.Id,
            kind,
            cost,
            sourceRuntime,
            targetRuntime,
            implementation);
    }

    private sealed record PathCandidate(
        TypeId Type,
        int Cost,
        IReadOnlyList<ValueConversionDescriptor> Steps,
        IReadOnlySet<TypeId> Visited);

    private sealed class PathCandidateComparer : IComparer<PathCandidate>
    {
        public static PathCandidateComparer Instance { get; } = new();

        public int Compare(PathCandidate? x, PathCandidate? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            int cost = x.Cost.CompareTo(y.Cost);
            return cost != 0 ? cost : string.CompareOrdinal(x.Type.Value, y.Type.Value);
        }
    }
}

internal static class SurfaceValue
{
    public static string Text(ValueLiteral literal)
    {
        if (literal.Value is string text)
        {
            return text;
        }
        string value = literal.Text;
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1]
                .Replace("\\\"", "\"")
                .Replace("\\'", "'")
                .Replace("\\\\", "\\");
        }
        if (value.Length >= 2 && value[0] == '{' && value[^1] == '}' && !value.Contains(':'))
        {
            return value[1..^1];
        }
        return literal.RawValue.ToString() ?? string.Empty;
    }
}

public sealed class TextValueCodec : IValueCodec<string>
{
    public string Parse(ValueLiteral literal, ValueParseContext context) => SurfaceValue.Text(literal);
    public string Format(string value, ValueFormatContext context) => value;
}

public sealed class BooleanValueCodec : IValueCodec<bool>
{
    public bool Parse(ValueLiteral literal, ValueParseContext context)
    {
        object value = literal.RawValue;
        if (value is bool boolean) return boolean;
        string text = SurfaceValue.Text(literal);
        if (bool.TryParse(text, out bool parsed)) return parsed;
        if (text.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
        throw new FormatException($"'{text}' is not a boolean.");
    }

    public string Format(bool value, ValueFormatContext context) => value ? "true" : "false";
}

public sealed class NumberValueCodec : IValueCodec<decimal>
{
    public decimal Parse(ValueLiteral literal, ValueParseContext context)
    {
        if (literal.RawValue is decimal number) return number;
        string text = SurfaceValue.Text(literal);
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : throw new FormatException($"'{text}' is not a number.");
    }

    public string Format(decimal value, ValueFormatContext context) =>
        value.ToString(CultureInfo.InvariantCulture);
}

public sealed class FileValueCodec : IValueCodec<FileInfo>
{
    public FileInfo Parse(ValueLiteral literal, ValueParseContext context) => new(SurfaceValue.Text(literal));
    public string Format(FileInfo value, ValueFormatContext context) => value.FullName;
}

public sealed class DirectoryValueCodec : IValueCodec<DirectoryInfo>
{
    public DirectoryInfo Parse(ValueLiteral literal, ValueParseContext context) => new(SurfaceValue.Text(literal));
    public string Format(DirectoryInfo value, ValueFormatContext context) => value.FullName;
}

public sealed class UriValueCodec : IValueCodec<Uri>
{
    public Uri Parse(ValueLiteral literal, ValueParseContext context)
    {
        string text = SurfaceValue.Text(literal);
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            ? uri
            : throw new FormatException($"'{text}' is not an absolute URI.");
    }

    public string Format(Uri value, ValueFormatContext context) => value.ToString();
}

public sealed class JsonElementValueCodec : IValueCodec<JsonElement>
{
    public JsonElement Parse(ValueLiteral literal, ValueParseContext context)
    {
        if (literal.RawValue is JsonElement element) return element.Clone();
        string json = literal.RawValue switch
        {
            string text => text,
            string[] lines => string.Join('\n', lines),
            _ => JsonSerializer.Serialize(literal.RawValue)
        };
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public string Format(JsonElement value, ValueFormatContext context) => value.GetRawText();
}

public sealed class EncodingValueCodec : IValueCodec<Encoding>
{
    public Encoding Parse(ValueLiteral literal, ValueParseContext context)
    {
        string text = SurfaceValue.Text(literal);
        return text.ToUpperInvariant() switch
        {
            "UTF8" or "UTF-8" => Encoding.UTF8,
            "UTF32" or "UTF-32" => Encoding.UTF32,
            "ASCII" => Encoding.ASCII,
            "UNICODE" => Encoding.Unicode,
            _ => Encoding.GetEncoding(text)
        };
    }

    public string Format(Encoding value, ValueFormatContext context) => value.WebName;
}

public sealed class TextToFileConversion : IValueConversion<string, FileInfo>
{
    public FileInfo Convert(string value, ValueConversionContext context) => new(value);
}

public sealed class TextToDirectoryConversion : IValueConversion<string, DirectoryInfo>
{
    public DirectoryInfo Convert(string value, ValueConversionContext context) => new(value);
}

public sealed class TextToUriConversion : IValueConversion<string, Uri>
{
    public Uri Convert(string value, ValueConversionContext context) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            ? uri
            : throw new FormatException($"'{value}' is not an absolute URI.");
}

public sealed class TextToNumberConversion : IValueConversion<string, decimal>
{
    public decimal Convert(string value, ValueConversionContext context) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : throw new FormatException($"'{value}' is not a number.");
}

public sealed class TextToBooleanConversion : IValueConversion<string, bool>
{
    public bool Convert(string value, ValueConversionContext context) =>
        new BooleanValueCodec().Parse(new ValueLiteral(value), new ValueParseContext(context.Language));
}

public sealed class TextToJsonConversion : IValueConversion<string, JsonElement>
{
    public JsonElement Convert(string value, ValueConversionContext context) =>
        new JsonElementValueCodec().Parse(new ValueLiteral(value), new ValueParseContext(context.Language));
}

public sealed class TextToEncodingConversion : IValueConversion<string, Encoding>
{
    public Encoding Convert(string value, ValueConversionContext context) =>
        new EncodingValueCodec().Parse(new ValueLiteral(value), new ValueParseContext(context.Language));
}

public sealed class NumberToTextConversion : IValueConversion<decimal, string>
{
    public string Convert(decimal value, ValueConversionContext context) =>
        value.ToString(CultureInfo.InvariantCulture);
}

public sealed class BooleanToTextConversion : IValueConversion<bool, string>
{
    public string Convert(bool value, ValueConversionContext context) => value ? "true" : "false";
}

public sealed class FileToTextConversion : IValueConversion<FileInfo, string>
{
    public string Convert(FileInfo value, ValueConversionContext context) => value.FullName;
}

public sealed class DirectoryToTextConversion : IValueConversion<DirectoryInfo, string>
{
    public string Convert(DirectoryInfo value, ValueConversionContext context) => value.FullName;
}

public sealed class UriToTextConversion : IValueConversion<Uri, string>
{
    public string Convert(Uri value, ValueConversionContext context) => value.ToString();
}

public sealed class JsonToTextConversion : IValueConversion<JsonElement, string>
{
    public string Convert(JsonElement value, ValueConversionContext context) => value.GetRawText();
}

public sealed class EncodingToTextConversion : IValueConversion<Encoding, string>
{
    public string Convert(Encoding value, ValueConversionContext context) => value.WebName;
}
