using FluNET.Keywords;
using System.Security.Cryptography.X509Certificates;

namespace FluNET.Syntax
{
    public interface IWord
    {
    }

    public interface IWhat<out TWhat> : INoun, IKeyword
    {
        TWhat What { get; }
    }

    public interface IFrom<out TWhat> : INoun, IKeyword
    {
        TWhat From { get; }
    }

    public interface IVerb : IKeyword
    {
        public Func<object> Act { get; }
    }

    public interface IVerb<TWhat, TFrom> : IKeyword
    {
        public Func<TFrom, TWhat> Act { get; }
    }

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

    public class GetText : Get<string[], FileInfo>
    {
        public GetText(string[] what, FileInfo from) : base(what, from)
        {
        }

        public override Func<FileInfo, string[]> Act
        {
            get
            {
                return (info) =>
                {
                    using (var reader = new StreamReader(info.OpenRead()))
                    {
                        return reader.ReadToEnd().Split('\n');
                    }
                };
            }
        }
    }
}