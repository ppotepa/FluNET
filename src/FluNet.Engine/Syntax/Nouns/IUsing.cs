using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    public interface IUsing<out TUsing> : INoun, IKeyword, IRole<TUsing>
    {
        TUsing Using { get; }
    }
}
