using FluNET.Language.Metadata;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using System.Reflection;

namespace FluNET.Language;

/// <summary>
/// Compiles CLR/reflection metadata into stable language descriptors. Reflection belongs
/// here (startup/build time), not in parser/binder hot paths.
/// </summary>
public sealed class LanguageCompiler
{
    private readonly NullabilityInfoContext _nullability = new();

    public VerbIdentity? DescribeVerbIdentity(Type verbType, IVerb? prototype = null)
    {
        VerbAttribute? explicitVerb = verbType.GetCustomAttribute<VerbAttribute>(true);
        string? text = explicitVerb?.Text
            ?? InferFamilyKeyword(verbType)
            ?? prototype?.Text;

        if (string.IsNullOrWhiteSpace(text))
            return null;

        string[] synonyms = verbType.GetCustomAttributes<AliasAttribute>(true)
            .Select(x => x.Value)
            .Concat(prototype?.Synonyms ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new VerbIdentity(text.ToUpperInvariant(), synonyms);
    }

    public VerbDescriptor DescribeVerb(
        Type verbType,
        string text,
        IReadOnlyList<string> synonyms,
        Func<IVerb?> factory)
    {
        IReadOnlyList<ConstructorDescriptor> constructors = DescribeConstructors(verbType);
        SentencePattern pattern = BuildPattern(verbType, text, constructors);

        return new VerbDescriptor(verbType, text, synonyms, pattern, factory)
        {
            Constructors = constructors,
            ResultType = InferResultType(verbType),
            FamilyType = InferFamilyType(verbType),
            Capabilities = verbType.GetCustomAttributes<RequiresCapabilityAttribute>(true)
                .Select(x => x.Capability)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public IReadOnlyList<ConstructorDescriptor> DescribeConstructors(Type type) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(constructor => new ConstructorDescriptor(
                constructor,
                constructor.GetParameters().Select(parameter => DescribeParameter(type, parameter)).ToArray()))
            .OrderByDescending(x => x.RoleParameterCount)
            .ThenBy(x => x.ServiceParameterCount)
            .ToArray();

    private ParameterDescriptor DescribeParameter(Type verbType, ParameterInfo parameter)
    {
        ClauseKind? role = InferRole(parameter);
        NullabilityInfo nullability = _nullability.Create(parameter);
        bool isParams = parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
        bool optional = parameter.IsOptional
            || parameter.HasDefaultValue
            || parameter.GetCustomAttribute<OptionalRoleAttribute>() != null
            || nullability.ReadState == NullabilityState.Nullable;

        return new ParameterDescriptor(
            parameter,
            parameter.Name ?? $"arg{parameter.Position}",
            parameter.ParameterType,
            role,
            InferDirection(verbType, parameter, role),
            optional,
            isParams,
            parameter.GetCustomAttribute<FromServicesAttribute>() != null,
            nullability.ReadState,
            nullability.WriteState,
            TypeShape.Analyze(parameter.ParameterType));
    }

    private static ClauseKind? InferRole(ParameterInfo parameter)
    {
        RoleAttribute? explicitRole = parameter.GetCustomAttribute<RoleAttribute>();
        if (explicitRole != null)
            return explicitRole.Kind;

        return parameter.Name?.ToLowerInvariant() switch
        {
            "what" => ClauseKind.What,
            "from" => ClauseKind.From,
            "to" => ClauseKind.To,
            "using" => ClauseKind.Using,
            "with" => ClauseKind.With,
            "then" => ClauseKind.Then,
            _ => null
        };
    }

    private static RoleDirection InferDirection(Type verbType, ParameterInfo parameter, ClauseKind? role)
    {
        if (parameter.GetCustomAttribute<OutputAttribute>() != null) return RoleDirection.Output;
        if (parameter.GetCustomAttribute<InputOutputAttribute>() != null) return RoleDirection.InputOutput;
        if (parameter.GetCustomAttribute<InputAttribute>() != null) return RoleDirection.Input;

        if (role == ClauseKind.What && IsFamily(verbType, typeof(IGet), "Get"))
            return RoleDirection.Output;

        return RoleDirection.Input;
    }

    private static SentencePattern BuildPattern(Type verbType, string text, IReadOnlyList<ConstructorDescriptor> constructors)
    {
        ConstructorDescriptor? constructor = constructors.FirstOrDefault(x => x.RoleParameterCount > 0);
        if (constructor != null)
        {
            ClauseDescriptor[] constructorClauses = constructor.Parameters
                .Where(x => x.Role != null)
                .Select(x => new ClauseDescriptor(
                    x.Role!.Value,
                    x.ParameterType,
                    !x.IsOptional,
                    x.Name,
                    x.Direction,
                    x.IsParams ? RoleCardinality.ZeroOrMore : (x.IsOptional ? RoleCardinality.ZeroOrOne : RoleCardinality.One),
                    x.Shape.ElementType))
                .ToArray();

            if (constructorClauses.Length > 0)
                return new SentencePattern(text.ToUpperInvariant(), constructorClauses);
        }

        List<ClauseDescriptor> clauses = [];
        foreach (Type contract in verbType.GetInterfaces().Where(x => x.IsGenericType))
        {
            Type definition = contract.GetGenericTypeDefinition();
            Type valueType = contract.GetGenericArguments()[0];
            ClauseKind? kind = RoleKindFor(definition);
            if (kind == null) continue;

            TypeShape shape = TypeShape.Analyze(valueType);
            RoleDirection direction = kind == ClauseKind.What && IsFamily(verbType, typeof(IGet), "Get")
                ? RoleDirection.Output
                : RoleDirection.Input;

            clauses.Add(new ClauseDescriptor(kind.Value, valueType, true, null, direction, RoleCardinality.One, shape.ElementType));
        }

        return new SentencePattern(text.ToUpperInvariant(), clauses);
    }

    private static ClauseKind? RoleKindFor(Type genericDefinition)
    {
        if (genericDefinition == typeof(IWhat<>)) return ClauseKind.What;
        if (genericDefinition == typeof(IFrom<>)) return ClauseKind.From;
        if (genericDefinition == typeof(ITo<>)) return ClauseKind.To;
        if (genericDefinition == typeof(IUsing<>)) return ClauseKind.Using;
        if (genericDefinition == typeof(IWith<>)) return ClauseKind.With;
        if (genericDefinition == typeof(IThen<>)) return ClauseKind.Then;
        return null;
    }

    private static Type? InferResultType(Type verbType)
    {
        Type? resultVerb = verbType.GetInterfaces().FirstOrDefault(x =>
            x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IVerb<>));
        if (resultVerb != null)
            return resultVerb.GetGenericArguments()[0];

        Type? legacyVerb = verbType.GetInterfaces().FirstOrDefault(x =>
            x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IVerb<,>));
        return legacyVerb?.GetGenericArguments()[0];
    }

    private static Type? InferFamilyType(Type verbType)
    {
        Type[] families = [typeof(IGet), typeof(ISave), typeof(ILoad), typeof(ISend), typeof(IDelete), typeof(IDownload), typeof(IPost), typeof(ITransform), typeof(ISay)];
        Type? marker = families.FirstOrDefault(x => x.IsAssignableFrom(verbType));
        if (marker != null) return marker;

        Type? current = verbType.BaseType;
        while (current != null && current != typeof(object))
        {
            Type candidate = current.IsGenericType ? current.GetGenericTypeDefinition() : current;
            if (KnownFamilyKeyword(candidate.Name) != null)
                return candidate;
            current = current.BaseType;
        }

        return null;
    }

    private static string? InferFamilyKeyword(Type verbType)
    {
        if (typeof(IGet).IsAssignableFrom(verbType)) return "GET";
        if (typeof(ISave).IsAssignableFrom(verbType)) return "SAVE";
        if (typeof(ILoad).IsAssignableFrom(verbType)) return "LOAD";
        if (typeof(ISend).IsAssignableFrom(verbType)) return "SEND";
        if (typeof(IDelete).IsAssignableFrom(verbType)) return "DELETE";
        if (typeof(IDownload).IsAssignableFrom(verbType)) return "DOWNLOAD";
        if (typeof(IPost).IsAssignableFrom(verbType)) return "POST";
        if (typeof(ITransform).IsAssignableFrom(verbType)) return "TRANSFORM";
        if (typeof(ISay).IsAssignableFrom(verbType)) return "SAY";

        Type? current = verbType.BaseType;
        while (current != null && current != typeof(object))
        {
            Type candidate = current.IsGenericType ? current.GetGenericTypeDefinition() : current;
            string? keyword = KnownFamilyKeyword(candidate.Name);
            if (keyword != null) return keyword;
            current = current.BaseType;
        }

        return null;
    }

    private static string? KnownFamilyKeyword(string typeName)
    {
        string name = typeName.Split('`')[0];
        return name.ToUpperInvariant() switch
        {
            "GET" => "GET",
            "SAVE" => "SAVE",
            "LOAD" => "LOAD",
            "SEND" => "SEND",
            "DELETE" => "DELETE",
            "DOWNLOAD" => "DOWNLOAD",
            "POST" => "POST",
            "TRANSFORM" => "TRANSFORM",
            "SAY" => "SAY",
            _ => null
        };
    }

    private static bool IsFamily(Type verbType, Type marker, string legacyBaseName)
    {
        if (marker.IsAssignableFrom(verbType)) return true;

        Type? current = verbType.BaseType;
        while (current != null && current != typeof(object))
        {
            Type candidate = current.IsGenericType ? current.GetGenericTypeDefinition() : current;
            if (candidate.Name.Split('`')[0].Equals(legacyBaseName, StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.BaseType;
        }

        return false;
    }
}
