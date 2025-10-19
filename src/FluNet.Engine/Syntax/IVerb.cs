using FluNET.Keywords;

namespace FluNET.Syntax
{
    public interface IVerb : IWord, IKeyword, IValidatable
    {
        public Func<object> Act { get; }
    }

    public interface IVerb<TWhat, TFrom> : IWord, IKeyword, IValidatable
    {
        public Func<TFrom, TWhat> Act { get; }
    }
}