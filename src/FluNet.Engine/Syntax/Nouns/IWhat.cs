using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    public interface IWhat<out TWhat> : INoun, IKeyword, IRole<TWhat>
    {
        TWhat What { get; }
    }
}
