using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    public interface IThen<out TData> : INoun, IKeyword, IRole<TData>
    {
        TData Data { get; }
    }
}
