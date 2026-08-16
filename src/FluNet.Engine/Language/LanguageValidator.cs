using FluNET.Diagnostics;

namespace FluNET.Language;

public static class LanguageValidator
{
    public static IReadOnlyList<Diagnostic> Validate(LanguageSnapshot snapshot)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (IGrouping<string, VerbDescriptor> group in snapshot.Verbs.GroupBy(SignatureKey, StringComparer.OrdinalIgnoreCase))
        {
            VerbDescriptor[] duplicates = group.ToArray();
            if (duplicates.Length > 1)
            {
                diagnostics.Add(Diagnostic.Error(
                    "FLU-LANG-001",
                    $"Duplicate verb signature '{group.Key}' is implemented by: {string.Join(", ", duplicates.Select(x => x.VerbType.FullName))}."));
            }
        }

        foreach (ModuleDescriptor module in snapshot.Modules)
        {
            foreach (Type dependency in module.Dependencies)
            {
                if (snapshot.Modules.All(x => x.ModuleType != dependency))
                {
                    diagnostics.Add(Diagnostic.Error(
                        "FLU-LANG-010",
                        $"Module '{module.ModuleName}' requires missing module '{dependency.FullName}'."));
                }
            }
        }

        return diagnostics;
    }

    private static string SignatureKey(VerbDescriptor verb) =>
        $"{verb.Text}:{string.Join("|", verb.Pattern.Clauses.Select(x => $"{x.Kind}:{x.ValueType.FullName}:{x.Cardinality}"))}";
}
