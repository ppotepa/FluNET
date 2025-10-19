using FluNET.Keywords;

namespace FluNET.Syntax
{
    public interface IWith<out TWith> : INoun, IKeyword
    {
        TWith With { get; }
    }
}
