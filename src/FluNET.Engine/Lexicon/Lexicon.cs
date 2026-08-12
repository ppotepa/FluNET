namespace FluNET.Lexicon
{
    public class Lexicon
    {
        private readonly Dictionary<Type, IEnumerable<VerbUsage>> verbUsages;
        private readonly DiscoveryService _discoveryService;

        public Lexicon(DiscoveryService discoveryService)
        {
            _discoveryService = discoveryService;
            verbUsages = [];
            BuildUsageDictionary();
        }

        private void BuildUsageDictionary()
        {
            List<Type> allVerbTypes = _discoveryService.Verbs
                .Where(t => t.BaseType != null && t.BaseType.IsGenericType)
                .ToList();

            IEnumerable<IGrouping<Type, Type>> groupedByBaseType = allVerbTypes
                .GroupBy(t => t.BaseType!.GetGenericTypeDefinition());

            foreach (IGrouping<Type, Type> group in groupedByBaseType)
            {
                Type baseVerbType = group.Key;
                List<VerbUsage> usages = group.Select(t => new VerbUsage
                {
                    ImplementationType = t,
                    UsageName = ExtractUsageName(t, baseVerbType),
                    FromType = ExtractFromType(t),
                    WhatType = ExtractWhatType(t)
                }).ToList();

                verbUsages[baseVerbType] = usages;
            }
        }

        private string ExtractUsageName(Type implementationType, Type baseVerbType)
        {
            string baseName = baseVerbType.Name;
            if (baseName.Contains("`"))
            {
                baseName = baseName[..baseName.IndexOf('`')];
            }

            string typeName = implementationType.Name;
            return typeName.StartsWith(baseName) ? typeName[baseName.Length..] : typeName;
        }

        private Type ExtractFromType(Type implementationType)
        {
            Type? baseType = implementationType.BaseType;
            if (baseType != null && baseType.IsGenericType)
            {
                Type[] genericArgs = baseType.GenericTypeArguments;
                if (genericArgs.Length >= 2)
                {
                    return genericArgs[1];
                }
            }
            return typeof(object);
        }

        private Type ExtractWhatType(Type implementationType)
        {
            Type? baseType = implementationType.BaseType;
            if (baseType != null && baseType.IsGenericType)
            {
                Type[] genericArgs = baseType.GenericTypeArguments;
                if (genericArgs.Length >= 1)
                {
                    return genericArgs[0];
                }
            }
            return typeof(object);
        }

        public IEnumerable<VerbUsage> this[Type baseVerbType]
        {
            get
            {
                return verbUsages.TryGetValue(baseVerbType, out IEnumerable<VerbUsage>? usages) ? usages : Enumerable.Empty<VerbUsage>();
            }
        }

        public IEnumerable<string> GetUsageNames(Type baseVerbType)
        {
            return this[baseVerbType].Select(u => u.UsageName);
        }

        public VerbUsage? FindUsage(Type baseVerbType, string usageName)
        {
            return this[baseVerbType].FirstOrDefault(u =>
                u.UsageName.Equals(usageName, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<Type> GetCompatibleFromTypes(Type baseVerbType, string usageName)
        {
            VerbUsage? usage = FindUsage(baseVerbType, usageName);
            if (usage != null)
            {
                yield return usage.FromType;
            }
        }
    }
}