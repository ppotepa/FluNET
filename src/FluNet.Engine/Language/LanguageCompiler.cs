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
        string[] synonyms = verbType.GetCustomAttributes<AliasAttribute>(true).Select(x => x.Value).Concat(prototype?.Synonyms ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new VerbIdentity(text.ToUpperInvariant(), synonyms);
    }

    public VerbDescriptor DescribeVerb(Type verbType, string text, IReadOnlyList<string> synonyms, Func<IVerb?> factory)
    {
        IReadOnlyList<ConstructorDescriptor> constructors = DescribeConstructors(verbType);
        IReadOnlyList<VerbPatternDescriptor> patterns = BuildPatterns(verbType, text, constructors);
        SentencePattern compatibilityPattern = patterns.FirstOrDefault()?.Pattern ?? BuildInterfacePattern(verbType, text);
        return new VerbDescriptor(verbType, text, synonyms, compatibilityPattern, factory)
        {
            Constructors = constructors,
            Patterns = patterns,
            ResultType = InferResultType(verbType),
            FamilyType = InferFamilyType(verbType),
            Capabilities = verbType.GetCustomAttributes<RequiresCapabilityAttribute>(true).Select(x => x.Capability).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Traits = new(typeof(IPureOperation).IsAssignableFrom(verbType), typeof(IIdempotentOperation).IsAssignableFrom(verbType), typeof(IRetryableOperation).IsAssignableFrom(verbType), typeof(ITransactionalOperation).IsAssignableFrom(verbType), typeof(ILongRunningOperation).IsAssignableFrom(verbType), typeof(ISideEffectingOperation).IsAssignableFrom(verbType))
        };
    }

    public IReadOnlyList<ConstructorDescriptor> DescribeConstructors(Type type) => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .Select(c => new ConstructorDescriptor(c, c.GetParameters().Select(p => DescribeParameter(type, p)).ToArray(), ConstructorActivatorCompiler.Compile(c)))
        .OrderByDescending(x => x.RoleParameterCount).ThenBy(x => x.ServiceParameterCount).ToArray();

    private IReadOnlyList<VerbPatternDescriptor> BuildPatterns(Type verbType, string text, IReadOnlyList<ConstructorDescriptor> constructors)
    {
        var patterns = new List<VerbPatternDescriptor>();
        foreach (ConstructorDescriptor constructor in constructors.Where(x => x.RoleParameterCount > 0))
        {
            ClauseDescriptor[] clauses = constructor.Parameters.Where(x => x.Role != null).Select(ToClause).ToArray();
            if (clauses.Length > 0) patterns.Add(new(new SentencePattern(text.ToUpperInvariant(), clauses), constructor));
        }
        if (patterns.Count == 0) patterns.Add(new(BuildInterfacePattern(verbType, text), constructors.FirstOrDefault()));
        return patterns.DistinctBy(x => PatternKey(x.Pattern), StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string PatternKey(SentencePattern pattern) => string.Join("|", pattern.Clauses.Select(x => $"{x.Kind}:{x.Name}:{x.ValueType.FullName}:{x.Cardinality}:{x.Direction}"));
    private static ClauseDescriptor ToClause(ParameterDescriptor p) => new(p.Role!.Value, p.ParameterType, !p.IsOptional, p.Name, p.Direction, p.IsParams ? RoleCardinality.ZeroOrMore : (p.IsOptional ? RoleCardinality.ZeroOrOne : RoleCardinality.One), p.Shape.ElementType);

    private SentencePattern BuildInterfacePattern(Type verbType, string text)
    {
        var fallback = new List<ClauseDescriptor>();
        foreach (Type contract in verbType.GetInterfaces().Where(x => x.IsGenericType))
        {
            Type definition = contract.GetGenericTypeDefinition(); Type valueType = contract.GetGenericArguments()[0]; ClauseKind? kind = RoleKindFor(definition); if (kind == null) continue;
            TypeShape shape = TypeShape.Analyze(valueType); RoleDirection direction = kind == ClauseKind.What && IsFamily(verbType, typeof(IGet), "Get") ? RoleDirection.Output : RoleDirection.Input;
            fallback.Add(new(kind.Value, valueType, true, null, direction, RoleCardinality.One, shape.ElementType));
        }
        return new SentencePattern(text.ToUpperInvariant(), fallback);
    }

    private ParameterDescriptor DescribeParameter(Type verbType, ParameterInfo p)
    {
        ClauseKind? role = InferRole(p); NullabilityInfo n = _nullability.Create(p); bool isParams = p.GetCustomAttribute<ParamArrayAttribute>() != null;
        bool optional = p.IsOptional || p.HasDefaultValue || p.GetCustomAttribute<OptionalRoleAttribute>() != null || n.ReadState == NullabilityState.Nullable;
        return new(p, p.Name ?? $"arg{p.Position}", p.ParameterType, role, InferDirection(verbType, p, role), optional, isParams, p.GetCustomAttribute<FromServicesAttribute>() != null, n.ReadState, n.WriteState, TypeShape.Analyze(p.ParameterType));
    }

    private static ClauseKind? InferRole(ParameterInfo p) { RoleAttribute? a = p.GetCustomAttribute<RoleAttribute>(); if (a != null) return a.Kind; return p.Name?.ToLowerInvariant() switch { "what" => ClauseKind.What, "from" => ClauseKind.From, "to" => ClauseKind.To, "using" => ClauseKind.Using, "with" => ClauseKind.With, "then" => ClauseKind.Then, _ => null }; }
    private static RoleDirection InferDirection(Type t, ParameterInfo p, ClauseKind? r) { if (p.GetCustomAttribute<OutputAttribute>() != null) return RoleDirection.Output; if (p.GetCustomAttribute<InputOutputAttribute>() != null) return RoleDirection.InputOutput; if (p.GetCustomAttribute<InputAttribute>() != null) return RoleDirection.Input; return r == ClauseKind.What && IsFamily(t, typeof(IGet), "Get") ? RoleDirection.Output : RoleDirection.Input; }
    private static ClauseKind? RoleKindFor(Type d) => d == typeof(IWhat<>) ? ClauseKind.What : d == typeof(IFrom<>) ? ClauseKind.From : d == typeof(ITo<>) ? ClauseKind.To : d == typeof(IUsing<>) ? ClauseKind.Using : d == typeof(IWith<>) ? ClauseKind.With : d == typeof(IThen<>) ? ClauseKind.Then : null;
    private static Type? InferResultType(Type t) => t.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IVerb<>))?.GetGenericArguments()[0];

    private static Type? InferFamilyType(Type t)
    {
        Type[] f = [typeof(IGet), typeof(ISave), typeof(ILoad), typeof(ISend), typeof(IDelete), typeof(IDownload), typeof(IPost), typeof(ITransform), typeof(ISay)]; Type? marker = f.FirstOrDefault(x => x.IsAssignableFrom(t)); if (marker != null) return marker;
        Type? c = t.BaseType; while (c != null && c != typeof(object)) { Type candidate = c.IsGenericType ? c.GetGenericTypeDefinition() : c; if (KnownFamilyKeyword(candidate.Name) != null) return candidate; c = c.BaseType; } return null;
    }

    private static string? InferFamilyKeyword(Type t)
    {
        if (typeof(IGet).IsAssignableFrom(t)) return "GET"; if (typeof(ISave).IsAssignableFrom(t)) return "SAVE"; if (typeof(ILoad).IsAssignableFrom(t)) return "LOAD"; if (typeof(ISend).IsAssignableFrom(t)) return "SEND"; if (typeof(IDelete).IsAssignableFrom(t)) return "DELETE"; if (typeof(IDownload).IsAssignableFrom(t)) return "DOWNLOAD"; if (typeof(IPost).IsAssignableFrom(t)) return "POST"; if (typeof(ITransform).IsAssignableFrom(t)) return "TRANSFORM"; if (typeof(ISay).IsAssignableFrom(t)) return "SAY";
        Type? c = t.BaseType; while (c != null && c != typeof(object)) { Type candidate = c.IsGenericType ? c.GetGenericTypeDefinition() : c; string? k = KnownFamilyKeyword(candidate.Name); if (k != null) return k; c = c.BaseType; } return null;
    }

    private static string? KnownFamilyKeyword(string n) => n.Split('`')[0].ToUpperInvariant() switch { "GET" => "GET", "SAVE" => "SAVE", "LOAD" => "LOAD", "SEND" => "SEND", "DELETE" => "DELETE", "DOWNLOAD" => "DOWNLOAD", "POST" => "POST", "TRANSFORM" => "TRANSFORM", "SAY" => "SAY", _ => null };
    private static bool IsFamily(Type t, Type marker, string legacy) { if (marker.IsAssignableFrom(t)) return true; Type? c = t.BaseType; while (c != null && c != typeof(object)) { Type candidate = c.IsGenericType ? c.GetGenericTypeDefinition() : c; if (candidate.Name.Split('`')[0].Equals(legacy, StringComparison.OrdinalIgnoreCase)) return true; c = c.BaseType; } return false; }
}
