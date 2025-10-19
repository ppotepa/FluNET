using System.Security.Cryptography.X509Certificates;

namespace FluNET.Syntax
{
    public interface IWord
    {
    }

    public interface INoun : IWord
    {        
    }

    public interface IWhat<out TWhat> : INoun
    {
        TWhat What { get; }
    }

    public interface IFrom<out TWhat> : INoun
    {
        TWhat From { get; }
    }

    public interface IVerb
    {
        public Func<object> Act { get; }        
    }

    public abstract class Get<TWhat, TFrom> : IVerb, 
        IWhat<TWhat>, 
        IFrom<TFrom>
        where TWhat : INoun
        where TFrom : INoun
    {
        protected Get(TWhat what, TFrom from)
        {
            this.What = what;
            this.From = from;
        }

        public abstract Func<object> Act { get; }

        public TWhat What { get; protected set; }

        public TFrom From { get; protected set; }
    }
}