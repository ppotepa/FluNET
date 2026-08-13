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
    public ConversionPath(
        IEnumerable<ValueConversionDescriptor> steps,
        int cost,
        bool isIdentity = false)
    {
        Steps = steps?.ToArray() ?? throw new ArgumentNullException(nameof(steps));
        Cost = cost;
        IsIdentity = isIdentity;
    }

    public IReadOnlyList<ValueConversionDescriptor> Steps { get; }
    public int Cost { get; }
    public bool IsIdentity { get; }
    public static ConversionPath Identity { get; } = new(Array.Empty<ValueConversionDescriptor>(), 0, true);
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
    IReadOnlyList<ValueConversionDescriptor> FindConversions(TypeId sourceType);
    ConversionResolution ResolveConversion(
        TypeSymbol source,
        TypeSymbol target,
        bool allowExplicit = false);
    object Convert(object value, ConversionPath path);
}

internal interface IRuntimeValueCodec
{
    ValueCodecDescriptor Descriptor { get; }
    object Parse(ValueLiteral literal, ValueParseContext context);
    string Format(object value, ValueFormatContext context);
}

internal sealed class RuntimeValueCodec<TValue> : IRuntimeValueCodec
{
    private readonly IValueCodec<TValue> _codec;

    public RuntimeValueCodec(TypeId typeId, IValueCodec<TValue> codec)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        Descriptor = new ValueCodecDescriptor(typeId, typeof(TValue), codec.GetType());
    }

    public ValueCodecDescriptor Descriptor { get; }

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

internal sealed class RuntimeValueConversion<TSource, TTarget> : IRuntimeValueConversion
{
    private readonly IValueConversion<TSource, TTarget> _conversion;

    public RuntimeValueConversion(
        ValueConversionDescriptor descriptor,
        IValueConversion<TSource, TTarget> conversion)
    {
        Descriptor = descriptor;
        _conversion = conversion ?? throw new ArgumentNullException(nameof(conversion));
    }

    public ValueConversionDescriptor Descriptor { get; }

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
    private readonly Dictionary<TypeId, IRuntimeValueCodec> _codecs = new();
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
        _codecs.Values
            .Select(codec => codec.Descriptor)
            .OrderBy(codec => codec.TypeId.Value, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyCollection<ValueConversionDescriptor> Conversions =>
        _conversions.Values
            .Select(conversion => conversion.Descriptor)
            .OrderBy(conversion => conversion.Id, StringComparer.Ordinal)
            .ToArray();

    public object Parse(TypeId typeId, ValueLiteral literal)
    {
        if (!_codecs.TryGetValue(typeId, out IRuntimeValueCodec? codec))
        {
            throw new InvalidOperationException(
                $"No value codec is registered for language type '{typeId}'.");
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
            throw new InvalidOperationException(
                $"No value codec is registered for language type '{typeId}'.");
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
        if (IsDirectlyAssignable(target, source))
        {
            return new ConversionResolution(ConversionPath.Identity, false);
        }

        List<PathCandidate> pending =
        [
            new PathCandidate(
                source.Id,
                0,
                Array.Empty<ValueConversionDescriptor>(),
                new HashSet<TypeId> { source.Id })
        ];
        Dictionary<TypeId, int> bestSeen = new() { [source.Id] = 0 };
        List<ConversionPath> matches = new();
        int? bestTargetCost = null;

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

                List<ValueConversionDescriptor> steps = new(current.Steps) { edge };
                TypeSymbol? reached = _language.Types.Find(edge.TargetType);
                if (reached is not null && IsDirectlyAssignable(target, reached))
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
                HashSet<TypeId> visited = new(current.Visited) { edge.TargetType };
                pending.Add(new PathCandidate(edge.TargetType, cost, steps, visited));
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
                throw new InvalidOperationException(
                    $"Conversion '{descriptor.Id}' is not registered.");
            }
            current = conversion.Convert(current, context);
        }
        return current;
    }

    private static bool IsDirectlyAssignable(TypeSymbol target, TypeSymbol source)
    {
        if (target.Id == source.Id)
        {
            return true;
        }

        // Batch 9 kept non-Unit -> Text as a transitional rule. The value layer
        // deliberately does not treat that compatibility rule as identity.
        if (target.Id == BuiltInTypeIds.Text)
        {
            return false;
        }
        return target.IsAssignableFrom(source);
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
        TypeSymbol encoding = _language.Types.Get(typeof(System.Text.Encoding));
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
        HashSet<TypeId> Visited);

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
