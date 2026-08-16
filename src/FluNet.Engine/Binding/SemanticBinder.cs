using FluNET.Diagnostics;
using FluNET.Language;
using FluNET.Language.Metadata;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Core;

namespace FluNET.Binding;

public sealed class SemanticBinder
{
    private readonly LanguageSnapshot _language;
    private readonly ValueResolverRegistry _resolvers;

    public SemanticBinder(LanguageSnapshot language, ValueResolverRegistry? resolvers = null)
    {
        _language = language;
        _resolvers = resolvers ?? new ValueResolverRegistry();
    }

    public BindingResult<BoundSentence> BindSentence(SentenceNode sentence, BindingContext? context = null)
    {
        context ??= new BindingContext();
        IReadOnlyList<VerbDescriptor> overloads = _language.GetVerbOverloads(sentence.Verb);
        if (overloads.Count == 0) return Failure("FLU2001", $"Unknown verb '{sentence.Verb}'.");

        var candidates = new List<BoundSentence>();
        foreach (VerbDescriptor overload in overloads)
        {
            BoundSentence? candidate = TryBindOverload(sentence, overload, context);
            if (candidate != null) candidates.Add(candidate);
        }

        if (candidates.Count == 0)
        {
            string signatures = string.Join(", ", overloads.Select(FormatSignature));
            return Failure("FLU2101", $"No overload of '{sentence.Verb}' matches this sentence. Available: {signatures}.");
        }

        int bestCost = candidates.Min(x => x.BindingCost);
        BoundSentence[] best = candidates.Where(x => x.BindingCost == bestCost).ToArray();
        if (best.Length > 1) return Failure("FLU2102", $"Ambiguous '{sentence.Verb}' sentence. Matching overloads: {string.Join(", ", best.Select(x => FormatSignature(x.Verb)))}.");
        return new(best[0], []);
    }

    public BindingResult<BoundPipeline> BindPipeline(PipelineNode pipeline, BindingContext? context = null)
    {
        context ??= new BindingContext();
        var bound = new List<BoundSentence>();
        var diagnostics = new List<Diagnostic>();
        var variableTypes = context.VariableTypes != null
            ? new Dictionary<string, Type>(context.VariableTypes, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        Type? pipelineType = context.PipelineType;

        foreach (SentenceNode sentence in pipeline.Sentences)
        {
            BindingContext sentenceContext = context with { PipelineType = pipelineType, VariableTypes = variableTypes };
            BindingResult<BoundSentence> result = BindSentence(sentence, sentenceContext);
            diagnostics.AddRange(result.Diagnostics);
            if (!result.Success || result.Value == null) return new(null, diagnostics);

            bound.Add(result.Value);
            pipelineType = result.Value.ResultType;

            foreach (BoundRole role in result.Value.Roles.Where(x => x.Descriptor.Direction is RoleDirection.Output or RoleDirection.InputOutput))
            {
                foreach (BoundValue value in role.Values)
                    if (value.Source is VariableExpression variable) variableTypes[variable.Name] = role.Descriptor.ValueType;
            }
        }

        return new(new BoundPipeline(bound, pipelineType), diagnostics);
    }

    private BoundSentence? TryBindOverload(SentenceNode sentence, VerbDescriptor verb, BindingContext context)
    {
        var remaining = sentence.Clauses.GroupBy(x => x.Kind).ToDictionary(x => x.Key, x => new Queue<ClauseNode>(x));
        var roles = new List<BoundRole>();
        int cost = 0;

        foreach (ClauseDescriptor expected in verb.Pattern.Clauses)
        {
            int minimum = expected.Cardinality is RoleCardinality.One or RoleCardinality.OneOrMore ? 1 : 0;
            bool repeated = expected.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore;
            var values = new List<BoundValue>();

            if (remaining.TryGetValue(expected.Kind, out Queue<ClauseNode>? queue) && queue.Count > 0)
            {
                if (repeated && expected.ElementType != null)
                {
                    BoundValue? collection = TryBindRepeatedValues(queue, expected, verb, context);
                    if (collection == null) return null;
                    values.Add(collection); cost += collection.ConversionCost;
                }
                else
                {
                    ClauseNode actual = queue.Dequeue();
                    BoundValue? value = TryBindValue(actual.Value, expected, verb, context);
                    if (value == null) return null;
                    values.Add(value); cost += value.ConversionCost;
                }
            }

            if (values.Count < minimum)
            {
                BoundValue? implicitPipeline = TryBindPipelineValue(expected, context);
                if (implicitPipeline != null) { values.Add(implicitPipeline); cost += implicitPipeline.ConversionCost; }
            }

            if (values.Count < minimum) return null;
            roles.Add(new BoundRole(expected, values));
        }

        if (remaining.Values.Any(queue => queue.Count > 0)) return null;
        ConstructorDescriptor? constructor = verb.Constructors.FirstOrDefault(x => x.RoleParameterCount > 0) ?? verb.Constructors.FirstOrDefault();
        return new BoundSentence(verb, constructor, roles, verb.ResultType, cost);
    }

    private BoundValue? TryBindRepeatedValues(Queue<ClauseNode> queue, ClauseDescriptor expected, VerbDescriptor verb, BindingContext context)
    {
        ClauseNode[] clauses = queue.ToArray(); queue.Clear();
        string[] texts = clauses.Select(x => x.Value switch { LiteralExpression l => l.Value, ReferenceExpression r => r.Reference, _ => null }).Where(x => x != null).Cast<string>().ToArray();
        if (texts.Length != clauses.Length) return null;
        ResolutionContext resolution = new(expected.ValueType, expected.Kind, verb, Qualifier: null, Services: context.Services);
        if (!_resolvers.TryResolveMany(texts, expected.ValueType, resolution, out object? collection)) return null;
        ExpressionNode source = clauses.Length == 1 ? clauses[0].Value : new InterpolatedStringExpression(string.Join(" ", texts));
        return new(source, expected.ValueType, collection?.GetType() ?? expected.ValueType, collection, 2);
    }

    private BoundValue? TryBindValue(ExpressionNode expression, ClauseDescriptor expected, VerbDescriptor verb, BindingContext context)
    {
        if (expected.Direction == RoleDirection.Output && expression is VariableExpression output) return new(output, expected.ValueType, expected.ValueType, null, 0);
        if (expression is PipelineValueExpression) return BindKnownType(expression, context.PipelineType, expected.ValueType, null);
        if (expression is VariableExpression variable)
        {
            Type? actualType = null; context.VariableTypes?.TryGetValue(variable.Name, out actualType);
            return BindKnownType(expression, actualType, expected.ValueType, null);
        }
        if (expression is InterpolatedStringExpression interpolated && expected.ValueType == typeof(string)) return new(interpolated, typeof(string), typeof(string), null, 0);
        string? text = expression switch { LiteralExpression l => l.Value, ReferenceExpression r => r.Reference, _ => null };
        if (text == null) return null;
        ResolutionContext resolution = new(expected.ValueType, expected.Kind, verb, Qualifier: null, Services: context.Services);
        if (_resolvers.TryResolve(text, expected.ValueType, resolution, out object? value)) return new(expression, expected.ValueType, value?.GetType() ?? expected.ValueType, value, expected.ValueType == typeof(string) ? 0 : 2);
        return null;
    }

    private static BoundValue? TryBindPipelineValue(ClauseDescriptor expected, BindingContext context)
    {
        if (expected.Direction == RoleDirection.Output || context.PipelineType == null) return null;
        return BindKnownType(new PipelineValueExpression(), context.PipelineType, expected.ValueType, null);
    }

    private static BoundValue? BindKnownType(ExpressionNode source, Type? actualType, Type expectedType, object? value)
    {
        if (actualType == null) return null;
        if (expectedType == actualType) return new(source, expectedType, actualType, value, 0);
        if (expectedType.IsAssignableFrom(actualType)) return new(source, expectedType, actualType, value, 1);
        return null;
    }

    private static BindingResult<BoundSentence> Failure(string code, string message) => new(null, [Diagnostic.Error(code, message)]);
    private static string FormatSignature(VerbDescriptor verb) { string clauses = string.Join(" ", verb.Pattern.Clauses.Select(x => $"{x.Kind.ToString().ToUpperInvariant()}<{FriendlyName(x.ValueType)}>")); return string.IsNullOrEmpty(clauses) ? verb.Text : $"{verb.Text} {clauses}"; }
    private static string FriendlyName(Type type) => type.IsArray ? $"{FriendlyName(type.GetElementType()!)}[]" : type.Name;
}
