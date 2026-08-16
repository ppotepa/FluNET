using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    public interface ITo<out TTo> : INoun, IKeyword, IRole<TTo>
    {
        TTo To { get; }
    }
}
