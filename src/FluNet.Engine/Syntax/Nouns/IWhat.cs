using FluNET.Keywords;

namespace FluNET.Syntax
{
    /// <summary>
    /// Represents a direct object in a sentence - the thing being acted upon by a verb.
    /// Example: In "GET [data] FROM file", [data] implements IWhat&lt;string[]&gt;.
    /// </summary>
    /// <typeparam name="TWhat">The type of the direct object</typeparam>
    public interface IWhat<out TWhat> : INoun, IKeyword
    {
        /// <summary>
        /// The direct object value being acted upon.
        /// </summary>
        TWhat What { get; }
    }
}
