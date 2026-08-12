namespace FluNET.Lexicon
{
    public class VerbUsage
    {
        public required Type ImplementationType { get; init; }
        public required string UsageName { get; init; }
        public required Type FromType { get; init; }
        public required Type WhatType { get; init; }

        public bool AcceptsFromType(Type sourceType)
        {
            return FromType.IsAssignableFrom(sourceType);
        }

        public bool ProducesWhatType(Type targetType)
        {
            return WhatType.IsAssignableFrom(targetType);
        }
    }
}
