using FluNET.Keywords;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns
{
    /// <summary>
    /// Represents an accompaniment preposition - what accompanies or is used with an action.
    /// Example: In "CONNECT TO server WITH [credentials]", [credentials] implements IWith&lt;AuthToken&gt;.
    /// </summary>
    /// <typeparam name="TWith">The type of the accompanying element</typeparam>
    public interface IWith<out TWith> : INoun, IKeyword
    {
        /// <summary>
        /// The accompanying element used with the action.
        /// </summary>
        TWith With { get; }
    }
}