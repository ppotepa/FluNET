using FluNET.Syntax;

namespace FluNET.Lexicon
{
    public class Lexicon
    {
        private readonly Dictionary<Type, IEnumerable<VerbUsage>> verbUsages;

        public Lexicon(DiscoveryService discoveryService)
        {
            verbUsages = new Dictionary<Type, IEnumerable<VerbUsage>>();
            BuildUsageDictionary(discoveryService);
        }

        private void BuildUsageDictionary(DiscoveryService discoveryService)
        {
            var allVerbTypes = discoveryService.Verbs
                .Where(t => t.BaseType != null && t.BaseType.IsGenericType)
                .ToList();

            var groupedByBaseType = allVerbTypes
                .GroupBy(t => t.BaseType.GetGenericTypeDefinition());

            foreach (var group in groupedByBaseType)
            {
                var baseVerbType = group.Key;
                var usages = group.Select(t => new VerbUsage
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
                baseName = baseName.Substring(0, baseName.IndexOf('`'));
            }
            
            string typeName = implementationType.Name;
            if (typeName.StartsWith(baseName))
            {
                return typeName.Substring(baseName.Length);
            }
            
            return typeName;
        }
        
        private Type ExtractFromType(Type implementationType)
        {
            var baseType = implementationType.BaseType;
            if (baseType != null && baseType.IsGenericType)
            {
                var genericArgs = baseType.GenericTypeArguments;
                if (genericArgs.Length >= 2)
                {
                    return genericArgs[1];
                }
            }
            return typeof(object);
        }
        
        private Type ExtractWhatType(Type implementationType)
        {
            var baseType = implementationType.BaseType;
            if (baseType != null && baseType.IsGenericType)
            {
                var genericArgs = baseType.GenericTypeArguments;
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
                if (verbUsages.TryGetValue(baseVerbType, out var usages))
                {
                    return usages;
                }
                return Enumerable.Empty<VerbUsage>();
            }
        }
        
        public IEnumerable<string> GetUsageNames(Type baseVerbType)
        {
            return this[baseVerbType].Select(u => u.UsageName);
        }
        
        public VerbUsage FindUsage(Type baseVerbType, string usageName)
        {
            return this[baseVerbType].FirstOrDefault(u => 
                u.UsageName.Equals(usageName, StringComparison.OrdinalIgnoreCase));
        }
        
        public IEnumerable<Type> GetCompatibleFromTypes(Type baseVerbType, string usageName)
        {
            var usage = FindUsage(baseVerbType, usageName);
            if (usage != null)
            {
                yield return usage.FromType;
            }
        }
    }
}
