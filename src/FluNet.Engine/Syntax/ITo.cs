using FluNET.Keywords;

namespace FluNET.Syntax
{
    public interface ITo<out TTo> : INoun, IKeyword
    {
        TTo To { get; }
    }
}
