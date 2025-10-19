namespace FluNET.Lexicon
{
    public class VerbUsage
    {
        public Type ImplementationType { get; set; }
        public string UsageName { get; set; }
        public Type FromType { get; set; }
        public Type WhatType { get; set; }
        
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
