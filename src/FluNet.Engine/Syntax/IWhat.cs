using FluNET.Keywords;

namespace FluNET.Syntax
{
    public interface IWhat<out TWhat> : INoun, IKeyword
    {
        TWhat What { get; }
    }
}
