namespace FluNET.Lexicon
{
    public class VerbUsage
    {
        public required Type ImplementationType { get; set; }
        public required string UsageName { get; set; }
        public required Type FromType { get; set; }
        public required Type WhatType { get; set; }

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