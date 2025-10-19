namespace FluNET.Syntax.Verbs
{
    public abstract class Get<TWhat, TFrom> : IVerb<TWhat, TFrom>,
        IWhat<TWhat>,
        IFrom<TFrom>
    {
        protected Get(TWhat what, TFrom from)
        {
            this.What = what;
            this.From = from;
        }

        public TWhat What { get; protected set; }

        public TFrom From { get; protected set; }

        public string Text => "GET";

        public abstract Func<TFrom, TWhat> Act { get; }

        public ValidationResult ValidateNext(string nextTokenValue, DiscoveryService discoveryService)
        {
            // Find all implementations of Get verb (GetText, GetData, etc.)
            var getImplementations = discoveryService.Verbs
                .Where(t => t.BaseType != null && 
                           t.BaseType.IsGenericType && 
                           t.BaseType.GetGenericTypeDefinition() == typeof(Get<,>))
                .ToList();

            // Extract the noun part from each implementation (e.g., "Text" from "GetText")
            var validNouns = getImplementations
                .Select(t => t.Name.Substring(3)) // Remove "Get" prefix
                .ToList();

            // Check if the next token matches any valid noun
            if (validNouns.Any(n => n.Equals(nextTokenValue, StringComparison.OrdinalIgnoreCase)))
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"Invalid noun '{nextTokenValue}' after GET verb. Valid options are: {string.Join(", ", validNouns)}");
        }
    }
}
