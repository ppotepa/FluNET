using System.Text.Json;

namespace FluNET.Language;

public sealed record LanguageManifest(IReadOnlyList<object> Verbs, IReadOnlyList<object> Qualifiers, IReadOnlyList<object> Modules);

public static class LanguageIntrospection
{
    public static LanguageManifest CreateManifest(LanguageSnapshot snapshot) => new(
        snapshot.Verbs.Select(v => (object)new
        {
            id = v.StableId,
            keyword = v.Text,
            synonyms = v.Synonyms,
            resultType = v.ResultType?.FullName,
            family = v.FamilyType?.FullName,
            capabilities = v.Capabilities,
            traits = v.Traits,
            patterns = (v.Patterns.Count > 0 ? v.Patterns.Select(x => x.Pattern) : [v.Pattern]).Select(pattern => new
            {
                clauses = pattern.Clauses.Select(c => new
                {
                    kind = c.Kind.ToString().ToUpperInvariant(),
                    name = c.Name,
                    valueType = c.ValueType.FullName,
                    elementType = c.ElementType?.FullName,
                    direction = c.Direction.ToString(),
                    cardinality = c.Cardinality.ToString(),
                    required = c.Required
                }).ToArray()
            }).ToArray()
        }).ToArray(),
        snapshot.Qualifiers.Select(q => (object)new { id = q.StableId, text = q.Text, valueType = q.ValueType?.FullName }).ToArray(),
        snapshot.Modules.Select(m => (object)new { id = m.StableId, name = m.ModuleName, version = m.Version.ToString(), dependencies = m.Dependencies.Select(x => x.FullName).ToArray() }).ToArray());

    public static string ToJson(LanguageSnapshot snapshot, bool indented = true) => JsonSerializer.Serialize(CreateManifest(snapshot), new JsonSerializerOptions { WriteIndented = indented });

    public static string ExplainVerb(LanguageSnapshot snapshot, string keyword)
    {
        IReadOnlyList<VerbDescriptor> overloads = snapshot.GetVerbOverloads(keyword);
        if (overloads.Count == 0) return $"Unknown verb: {keyword}";
        return string.Join(Environment.NewLine + Environment.NewLine, overloads.SelectMany(v =>
        {
            IEnumerable<SentencePattern> patterns = v.Patterns.Count > 0 ? v.Patterns.Select(x => x.Pattern) : [v.Pattern];
            return patterns.Select(pattern =>
            {
                string signature = string.Join(" ", pattern.Clauses.Select(c => $"{c.Kind.ToString().ToUpperInvariant()}<{c.ValueType.Name}>"));
                return $"{v.Text} {signature}\nImplementation: {v.VerbType.FullName}\nResult: {v.ResultType?.FullName ?? "void/unknown"}\nCapabilities: {string.Join(", ", v.Capabilities)}";
            });
        }));
    }
}
