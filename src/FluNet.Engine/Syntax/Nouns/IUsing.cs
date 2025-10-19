using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    /// <summary>
    /// Represents an instrument preposition - the tool, method, or means by which an action is performed.
    /// Example: In "ENCRYPT data USING [AES256]", [AES256] implements IUsing&lt;Algorithm&gt;.
    /// </summary>
    /// <typeparam name="TUsing">The type of the instrument or method</typeparam>
    public interface IUsing<out TUsing> : INoun, IKeyword
    {
        /// <summary>
        /// The instrument, tool, or method used to perform the action.
        /// </summary>
        TUsing Using { get; }
    }
}