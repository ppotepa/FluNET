namespace FluNET.Syntax
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
    }
}
