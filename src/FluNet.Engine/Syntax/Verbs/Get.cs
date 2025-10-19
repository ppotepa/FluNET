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

        public ValidationResult ValidateNext(string nextTokenValue, Lexicon.Lexicon lexicon)
        {
            var validUsages = lexicon.GetUsageNames(typeof(Get<,>));

            if (validUsages.Any(n => n.Equals(nextTokenValue, StringComparison.OrdinalIgnoreCase)))
            {
                return ValidationResult.Success();
            }

            if (nextTokenValue.Equals("FROM", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"Invalid noun '{nextTokenValue}' after GET verb. Valid options are: {string.Join(", ", validUsages)}");
        }
    }
}
