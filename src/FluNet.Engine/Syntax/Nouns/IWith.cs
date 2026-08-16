using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    public interface IWith<out TWith> : INoun, IKeyword, IRole<TWith>
    {
        TWith With { get; }
    }
}
