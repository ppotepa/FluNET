using FluNET.Keywords;

namespace FluNET.Syntax.Core
{
    public interface IVerb : IWord, IKeyword
    {
        string[] Synonyms => Array.Empty<string>();
    }

    /// <summary>
    /// Result-only semantic verb contract. New verbs can compose this with IGet/ISave/etc.
    /// and independent role interfaces without being forced into a fixed generic arity.
    /// </summary>
    public interface IVerb<out TResult> : IVerb
    {
    }

    /// <summary>
    /// Legacy two-type execution contract kept for Classic compatibility. It now also
    /// projects its result type through IVerb&lt;TResult&gt; so new metadata code can reason
    /// about old and new verbs uniformly.
    /// </summary>
    public interface IVerb<TWhat, TFrom> : IVerb<TWhat>
    {
        Func<TFrom, TWhat> Act { get; }
        TWhat Invoke();
        TFrom? Resolve(string value);
    }
}
