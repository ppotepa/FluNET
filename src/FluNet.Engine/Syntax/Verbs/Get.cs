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
            var getImplementations = discoveryService.Verbs
                .Where(t => t.BaseType != null &&
                           t.BaseType.IsGenericType &&
                           t.BaseType.GetGenericTypeDefinition() == typeof(Get<,>))
                .ToList();

            var validNouns = getImplementations
                .Select(t => t.Name.Substring(3))
                .ToList();

            if (validNouns.Any(n => n.Equals(nextTokenValue, StringComparison.OrdinalIgnoreCase)))
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"Invalid noun '{nextTokenValue}' after GET verb. Valid options are: {string.Join(", ", validNouns)}");
        }
    }
}
