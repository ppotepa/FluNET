using FluNET.Keywords;

namespace FluNET.Syntax
{
    public interface IUsing<out TUsing> : INoun, IKeyword
    {
        TUsing Using { get; }
    }
}
