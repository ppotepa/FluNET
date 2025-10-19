using FluNET.Keywords;

namespace FluNET.Syntax
{
    public interface IFrom<out TWhat> : INoun, IKeyword
    {
        TWhat From { get; }
    }
}
