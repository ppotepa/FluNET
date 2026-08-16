using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    public interface IFrom<out TFrom> : INoun, IKeyword, IRole<TFrom>
    {
        TFrom From { get; }
    }
}
