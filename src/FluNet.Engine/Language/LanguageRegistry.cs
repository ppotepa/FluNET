using FluNET.Keywords;
using FluNET.Syntax.Core;
using System.Reflection;

namespace FluNET.Language;

public sealed class LanguageRegistry
{
    private readonly Dictionary<string, WordDescriptor> _words = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<VerbDescriptor>> _verbs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QualifierDescriptor> _qualifiers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ModuleDescriptor> _modules = [];
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly LanguageCompiler _compiler = new();

    public LanguageRegistry() { RegisterStandardQualifiers(); RegisterAssemblies(AppDomain.CurrentDomain.GetAssemblies()); }

    public IReadOnlyCollection<WordDescriptor> Words => _words.Values.DistinctBy(x => x.WordType).ToArray();
    public IReadOnlyCollection<VerbDescriptor> Verbs => _verbs.Values.SelectMany(x => x).DistinctBy(x => x.VerbType).ToArray();
    public IReadOnlyCollection<QualifierDescriptor> Qualifiers => _qualifiers.Values.ToArray();
    public IReadOnlyList<ModuleDescriptor> Modules => _modules;
    public LanguageSnapshot Snapshot => new(Words, Verbs, Qualifiers, Modules);

    public void RegisterModule(IFluNetModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_modules.All(x => x.ModuleType != module.GetType()))
            _modules.Add(new(module.Name, module.Version, module.GetType(), module.Dependencies.ToArray()));
        module.Configure(this);
        RegisterAssemblies([module.GetType().Assembly]);
    }

    public void RegisterAssemblies(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            if (!_assemblies.Add(assembly)) continue;
            Type[] types;
            try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(x => x != null).Cast<Type>().ToArray(); }
            foreach (Type type in types.Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface)) RegisterWord(type);
        }
    }

    public void Refresh() { _words.Clear(); _verbs.Clear(); _assemblies.Clear(); RegisterAssemblies(AppDomain.CurrentDomain.GetAssemblies()); }
    public void RegisterQualifier(string text, Type? valueType = null) { ArgumentException.ThrowIfNullOrWhiteSpace(text); _qualifiers[text] = new(text.ToUpperInvariant(), valueType); }
    public bool IsQualifier(string text) => _qualifiers.ContainsKey(text);
    public bool TryCreateWord(string text, out IWord? word) { if (_words.TryGetValue(text, out WordDescriptor? d)) { word = d.Factory(); return word != null; } word = null; return false; }
    public bool TryGetVerb(string text, out VerbDescriptor? descriptor) { descriptor = GetVerbOverloads(text).FirstOrDefault(); return descriptor != null; }
    public IReadOnlyList<VerbDescriptor> GetVerbOverloads(string text) => _verbs.TryGetValue(text, out List<VerbDescriptor>? overloads) ? overloads.DistinctBy(x => x.VerbType).ToArray() : [];

    public Type? GetVerbBaseType(string text)
    {
        VerbDescriptor? descriptor = GetVerbOverloads(text).FirstOrDefault(); if (descriptor == null) return null;
        Type? baseType = descriptor.VerbType.BaseType; while (baseType != null && !baseType.IsAbstract && baseType != typeof(object)) baseType = baseType.BaseType;
        if (baseType == null || baseType == typeof(object)) return null; return baseType.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;
    }

    private void RegisterWord(Type type)
    {
        Func<IWord?> factory = () => CreatePrototype(type) as IWord;
        IWord? prototype = factory();
        if (typeof(IVerb).IsAssignableFrom(type))
        {
            VerbIdentity? identity = _compiler.DescribeVerbIdentity(type, prototype as IVerb);
            if (identity != null)
            {
                VerbDescriptor descriptor = _compiler.DescribeVerb(type, identity.Text, identity.Synonyms, () => factory() as IVerb);
                RegisterOverload(identity.Text, descriptor); foreach (string synonym in identity.Synonyms) RegisterOverload(synonym, descriptor);
                if (prototype is IKeyword) { var word = new WordDescriptor(type, identity.Text, identity.Synonyms, factory); _words.TryAdd(identity.Text, word); foreach (string synonym in identity.Synonyms) _words.TryAdd(synonym, word); }
            }
            return;
        }
        if (prototype is IKeyword keyword) _words.TryAdd(keyword.Text, new(type, keyword.Text, [], factory));
    }

    private void RegisterOverload(string keyword, VerbDescriptor descriptor) { if (!_verbs.TryGetValue(keyword, out List<VerbDescriptor>? overloads)) _verbs[keyword] = overloads = []; if (overloads.All(x => x.VerbType != descriptor.VerbType)) overloads.Add(descriptor); }

    private static object? CreatePrototype(Type type)
    {
        try { ConstructorInfo? p = type.GetConstructor(Type.EmptyTypes); if (p != null) return p.Invoke(null); ConstructorInfo? c = type.GetConstructors().OrderBy(x => x.GetParameters().Length).FirstOrDefault(); if (c == null) return null; object?[] a = c.GetParameters().Select(x => x.ParameterType.IsValueType ? Activator.CreateInstance(x.ParameterType) : null).ToArray(); return c.Invoke(a); } catch { return null; }
    }

    private void RegisterStandardQualifiers() { RegisterQualifier("TEXT", typeof(string)); RegisterQualifier("JSON"); RegisterQualifier("XML"); RegisterQualifier("BINARY", typeof(byte[])); foreach (string q in new[] { "CSV", "HTML", "YAML", "IMAGE", "VIDEO", "AUDIO" }) RegisterQualifier(q); }
}
