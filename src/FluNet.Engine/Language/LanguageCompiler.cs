using FluNET.Language.Metadata;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using System.Reflection;

namespace FluNET.Language;

public sealed class LanguageCompiler
{
    private readonly NullabilityInfoContext _nullability = new();

    public VerbIdentity? DescribeVerbIdentity(Type verbType, IVerb? prototype = null)
    {
        VerbAttribute? explicitVerb = verbType.GetCustomAttribute<VerbAttribute>(true);
        string? text = explicitVerb?.Text ?? InferFamilyKeyword(verbType) ?? prototype?.Text;
        if (string.IsNullOrWhiteSpace(text)) return null;

        string[] synonyms = verbType.GetCustomAttributes<AliasAttribute>(true)
            .Select(x => x.Value)
            .Concat(prototype?.Synonyms ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new VerbIdentity(text.ToUpperInvariant(), synonyms);
    }

    public VerbDescriptor DescribeVerb(Type verbType, string text, IReadOnlyList<string> synonyms, Func<IVerb?> factory)
    {
        IReadOnlyList<ConstructorDescriptor> constructors = DescribeConstructors(verbType);
        SentencePattern pattern = BuildPattern(verbType, text, constructors);
        return new VerbDescriptor(verbType, text, synonyms, pattern, factory)
        {
            Constructors = constructors,
            ResultType = InferResultType(verbType),
            FamilyType = InferFamilyType(verbType),
            Capabilities = verbType.GetCustomAttributes<RequiresCapabilityAttribute>(true).Select(x => x.Capability).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Traits = new(
                typeof(IPureOperation).IsAssignableFrom(verbType),
                typeof(IIdempotentOperation).IsAssignableFrom(verbType),
                typeof(IRetryableOperation).IsAssignableFrom(verbType),
                typeof(ITransactionalOperation).IsAssignableFrom(verbType),
                typeof(ILongRunningOperation).IsAssignableFrom(verbType),
                typeof(ISideEffectingOperation).IsAssignableFrom(verbType))
        };
    }

    public IReadOnlyList<ConstructorDescriptor> DescribeConstructors(Type type) => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .Select(c => new ConstructorDescriptor(c, c.GetParameters().Select(p => DescribeParameter(type, p)).ToArray()))
        .OrderByDescending(x => x.RoleParameterCount).ThenBy(x => x.ServiceParameterCount).ToArray();

    private ParameterDescriptor DescribeParameter(Type verbType, ParameterInfo parameter)
    {
        ClauseKind? role = InferRole(parameter);
        NullabilityInfo nullability = _nullability.Create(parameter);
        bool isParams = parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
        bool optional = parameter.IsOptional || parameter.HasDefaultValue || parameter.GetCustomAttribute<OptionalRoleAttribute>() != null || nullability.ReadState == NullabilityState.Nullable;
        return new(parameter, parameter.Name ?? $"arg{parameter.Position}", parameter.ParameterType, role, InferDirection(verbType, parameter, role), optional, isParams, parameter.GetCustomAttribute<FromServicesAttribute>() != null, nullability.ReadState, nullability.WriteState, TypeShape.Analyze(parameter.ParameterType));
    }

    private static ClauseKind? InferRole(ParameterInfo parameter)
    {
        RoleAttribute? explicitRole = parameter.GetCustomAttribute<RoleAttribute>();
        if (explicitRole != null) return explicitRole.Kind;
        return parameter.Name?.ToLowerInvariant() switch { "what" => ClauseKind.What, "from" => ClauseKind.From, "to" => ClauseKind.To, "using" => ClauseKind.Using, "with" => ClauseKind.With, "then" => ClauseKind.Then, _ => null };
    }

    private static RoleDirection InferDirection(Type verbType, ParameterInfo parameter, ClauseKind? role)
    {
        if (parameter.GetCustomAttribute<OutputAttribute>() != null) return RoleDirection.Output;
        if (parameter.GetCustomAttribute<InputOutputAttribute>() != null) return RoleDirection.InputOutput;
        if (parameter.GetCustomAttribute<InputAttribute>() != null) return RoleDirection.Input;
        return role == ClauseKind.What && IsFamily(verbType, typeof(IGet), "Get") ? RoleDirection.Output : RoleDirection.Input;
    }

    private static SentencePattern BuildPattern(Type verbType, string text, IReadOnlyList<ConstructorDescriptor> constructors)
    {
        ConstructorDescriptor? constructor = constructors.FirstOrDefault(x => x.RoleParameterCount > 0);
        if (constructor != null)
        {
            ClauseDescriptor[] clauses = constructor.Parameters.Where(x => x.Role != null).Select(x => new ClauseDescriptor(x.Role!.Value, x.ParameterType, !x.IsOptional, x.Name, x.Direction, x.IsParams ? RoleCardinality.ZeroOrMore : (x.IsOptional ? RoleCardinality.ZeroOrOne : RoleCardinality.One), x.Shape.ElementType)).ToArray();
            if (clauses.Length > 0) return new SentencePattern(text.ToUpperInvariant(), clauses);
        }

        var fallback = new List<ClauseDescriptor>();
        foreach (Type contract in verbType.GetInterfaces().Where(x => x.IsGenericType))
        {
            Type definition = contract.GetGenericTypeDefinition();
            Type valueType = contract.GetGenericArguments()[0];
            ClauseKind? kind = RoleKindFor(definition);
            if (kind == null) continue;
            TypeShape shape = TypeShape.Analyze(valueType);
            RoleDirection direction = kind == ClauseKind.What && IsFamily(verbType, typeof(IGet), "Get") ? RoleDirection.Output : RoleDirection.Input;
            fallback.Add(new(kind.Value, valueType, true, null, direction, RoleCardinality.One, shape.ElementType));
        }
        return new SentencePattern(text.ToUpperInvariant(), fallback);
    }

    private static ClauseKind? RoleKindFor(Type d) => d == typeof(IWhat<>) ? ClauseKind.What : d == typeof(IFrom<>) ? ClauseKind.From : d == typeof(ITo<>) ? ClauseKind.To : d == typeof(IUsing<>) ? ClauseKind.Using : d == typeof(IWith<>) ? ClauseKind.With : d == typeof(IThen<>) ? ClauseKind.Then : null;

    private static Type? InferResultType(Type verbType) => verbType.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IVerb<>))?.GetGenericArguments()[0];

    private static Type? InferFamilyType(Type verbType)
    {
        Type[] families = [typeof(IGet), typeof(ISave), typeof(ILoad), typeof(ISend), typeof(IDelete), typeof(IDownload), typeof(IPost), typeof(ITransform), typeof(ISay)];
        Type? marker = families.FirstOrDefault(x => x.IsAssignableFrom(verbType));
        if (marker != null) return marker;
        Type? current = verbType.BaseType;
        while (current != null && current != typeof(object)) { Type candidate = current.IsGenericType ? current.GetGenericTypeDefinition() : current; if (KnownFamilyKeyword(candidate.Name) != null) return candidate; current = current.BaseType; }
        return null;
    }

    private static string? InferFamilyKeyword(Type verbType)
    {
        if (typeof(IGet).IsAssignableFrom(verbType)) return "GET"; if (typeof(ISave).IsAssignableFrom(verbType)) return "SAVE"; if (typeof(ILoad).IsAssignableFrom(verbType)) return "LOAD"; if (typeof(ISend).IsAssignableFrom(verbType)) return "SEND"; if (typeof(IDelete).IsAssignableFrom(verbType)) return "DELETE"; if (typeof(IDownload).IsAssignableFrom(verbType)) return "DOWNLOAD"; if (typeof(IPost).IsAssignableFrom(verbType)) return "POST"; if (typeof(ITransform).IsAssignableFrom(verbType)) return "TRANSFORM"; if (typeof(ISay).IsAssignableFrom(verbType)) return "SAY";
        Type? current = verbType.BaseType;
        while (current != null && current != typeof(object)) { Type candidate = current.IsGenericType ? current.GetGenericTypeDefinition() : current; string? keyword = KnownFamilyKeyword(candidate.Name); if (keyword != null) return keyword; current = current.BaseType; }
        return null;
    }

    private static string? KnownFamilyKeyword(string typeName) => typeName.Split('`')[0].ToUpperInvariant() switch { "GET" => "GET", "SAVE" => "SAVE", "LOAD" => "LOAD", "SEND" => "SEND", "DELETE" => "DELETE", "DOWNLOAD" => "DOWNLOAD", "POST" => "POST", "TRANSFORM" => "TRANSFORM", "SAY" => "SAY", _ => null };

    private static bool IsFamily(Type verbType, Type marker, string legacyBaseName)
    {
        if (marker.IsAssignableFrom(verbType)) return true;
        Type? current = verbType.BaseType;
        while (current != null && current != typeof(object)) { Type candidate = current.IsGenericType ? current.GetGenericTypeDefinition() : current; if (candidate.Name.Split('`')[0].Equals(legacyBaseName, StringComparison.OrdinalIgnoreCase)) return true; current = current.BaseType; }
        return false;
    }
}
