namespace FluNET.Language;

public sealed class LanguageSnapshot
{
    private readonly IReadOnlyDictionary<string, WordDescriptor> _words;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<VerbDescriptor>> _verbs;
    private readonly IReadOnlyDictionary<string, QualifierDescriptor> _qualifiers;
    private readonly IReadOnlyList<ModuleDescriptor> _modules;

    public LanguageSnapshot(IEnumerable<WordDescriptor> words, IEnumerable<VerbDescriptor> verbs, IEnumerable<QualifierDescriptor> qualifiers, IEnumerable<ModuleDescriptor>? modules = null)
    {
        var wordMap = new Dictionary<string, WordDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (WordDescriptor word in words) { wordMap[word.Text] = word; foreach (string synonym in word.Synonyms) wordMap[synonym] = word; }
        var verbMap = new Dictionary<string, List<VerbDescriptor>>(StringComparer.OrdinalIgnoreCase);
        foreach (VerbDescriptor verb in verbs) { AddVerb(verb.Text, verb); foreach (string synonym in verb.Synonyms) AddVerb(synonym, verb); }
        _words = wordMap;
        _verbs = verbMap.ToDictionary(x => x.Key, x => (IReadOnlyList<VerbDescriptor>)x.Value.DistinctBy(v => v.VerbType).ToArray(), StringComparer.OrdinalIgnoreCase);
        _qualifiers = qualifiers.ToDictionary(x => x.Text, StringComparer.OrdinalIgnoreCase);
        _modules = (modules ?? Array.Empty<ModuleDescriptor>()).ToArray();
        void AddVerb(string key, VerbDescriptor descriptor) { if (!verbMap.TryGetValue(key, out List<VerbDescriptor>? set)) verbMap[key] = set = []; set.Add(descriptor); }
    }

    public IReadOnlyCollection<WordDescriptor> Words => _words.Values.DistinctBy(x => x.WordType).ToArray();
    public IReadOnlyCollection<VerbDescriptor> Verbs => _verbs.Values.SelectMany(x => x).DistinctBy(x => x.VerbType).ToArray();
    public IReadOnlyCollection<QualifierDescriptor> Qualifiers => _qualifiers.Values.ToArray();
    public IReadOnlyList<ModuleDescriptor> Modules => _modules;
    public bool TryGetWord(string text, out WordDescriptor? descriptor) => _words.TryGetValue(text, out descriptor);
    public IReadOnlyList<VerbDescriptor> GetVerbOverloads(string text) => _verbs.TryGetValue(text, out IReadOnlyList<VerbDescriptor>? descriptors) ? descriptors : [];
    public bool IsQualifier(string text) => _qualifiers.ContainsKey(text);
    public bool TryGetQualifier(string text, out QualifierDescriptor? descriptor) => _qualifiers.TryGetValue(text, out descriptor);
}
