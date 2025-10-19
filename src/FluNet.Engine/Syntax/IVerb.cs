using FluNET.Keywords;

namespace FluNET.Syntax
{
    public interface IVerb : IWord, IKeyword
    {
        public Func<object> Act { get; }
    }

    public interface IVerb<TWhat, TFrom> : IWord, IKeyword
    {
        public Func<TFrom, TWhat> Act { get; }
    }
}