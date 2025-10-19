using FluNET.Syntax.Validation;

namespace FluNET.Keywords
{
    public interface IKeyword : IValidatable
    {
        string Text { get; }
    }
}