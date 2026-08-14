namespace FluNET.Language.Contracts;

public sealed record ReleaseContractIssue(string Code, string Message);

public sealed record ReleaseContractReport(
    string CandidateVersion,
    string PublicLanguageVersion,
    IReadOnlyList<ReleaseContractIssue> Issues)
{
    public bool IsStaticallyValid => Issues.Count == 0;
    public bool IsPublicVersionAligned => PublicLanguageVersion == CandidateVersion;
}

/// <summary>
/// Side-effect-free 1.0 candidate invariants. This is not a substitute for restore/build/test.
/// </summary>
public static class ReleaseCandidateVerifier
{
    public static ReleaseContractReport Verify1_0(LanguageSnapshot language)
    {
        ArgumentNullException.ThrowIfNull(language);
        List<ReleaseContractIssue> issues = [];
        LanguageContractManifest languageContract = LanguageContractManifest.Create(language, StandardLanguageIdentity.Version);
        ExtensionApiContractManifest extensionContract = ExtensionApiContractManifest.Candidate1_0;

        Duplicate(languageContract.Frames.Select(frame => frame.FrameId), "RC001", "frame id", issues);
        Duplicate(languageContract.Types.Select(type => type.TypeId), "RC002", "type id", issues);
        Duplicate(extensionContract.Entries.Select(entry => $"{entry.Category}:{entry.ContractName}"), "RC003", "extension contract", issues);
        Duplicate(FluNetPlatformTopology.Modules.Select(module => module.Id), "RC004", "platform module id", issues);

        HashSet<string> modules = FluNetPlatformTopology.Modules.Select(module => module.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (PlatformModuleBoundary module in FluNetPlatformTopology.Modules)
        {
            foreach (string dependency in module.DependsOn)
                if (!modules.Contains(dependency))
                    issues.Add(new("RC005", $"Module '{module.Id}' depends on unknown module '{dependency}'."));
        }

        if (HasTopologyCycle())
            issues.Add(new("RC006", "Platform module dependency graph contains a cycle."));

        PlatformModuleBoundary compatibility = FluNetPlatformTopology.Get("flunet.compatibility");
        foreach (PlatformModuleBoundary module in FluNetPlatformTopology.Modules.Where(module => module.Kind != PlatformModuleKind.Compatibility))
            if (module.DependsOn.Contains(compatibility.Id, StringComparer.OrdinalIgnoreCase))
                issues.Add(new("RC007", $"Preferred module '{module.Id}' depends on compatibility layer."));

        string[] requiredSeparators = [",", ";", "newline", "|", "AND", "THEN"];
        foreach (string separator in requiredSeparators)
            if (!languageContract.Separators.Any(item => item.Token == separator))
                issues.Add(new("RC008", $"Language contract is missing separator '{separator}'."));

        return new("1.0", StandardLanguageIdentity.Version.Value, issues);
    }

    private static void Duplicate(IEnumerable<string> values, string code, string label, ICollection<ReleaseContractIssue> issues)
    {
        foreach (string duplicate in values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key))
            issues.Add(new(code, $"Duplicate {label}: '{duplicate}'."));
    }

    private static bool HasTopologyCycle()
    {
        Dictionary<string, int> state = new(StringComparer.OrdinalIgnoreCase);
        bool Visit(string id)
        {
            if (state.TryGetValue(id, out int existing)) return existing == 1;
            state[id] = 1;
            foreach (string dependency in FluNetPlatformTopology.Get(id).DependsOn)
                if (Visit(dependency)) return true;
            state[id] = 2;
            return false;
        }
        return FluNetPlatformTopology.Modules.Any(module => Visit(module.Id));
    }
}
