using FluNET.Keywords;

namespace FluNET.Syntax
{
    /// <summary>
    /// Represents a source or origin preposition - where something comes from.
    /// Example: In "GET data FROM [file.txt]", [file.txt] implements IFrom&lt;FileInfo&gt;.
    /// </summary>
    /// <typeparam name="TWhat">The type of the source/origin</typeparam>
    public interface IFrom<out TWhat> : INoun, IKeyword
    {
        /// <summary>
        /// The source or origin from which data is retrieved.
        /// </summary>
        TWhat From { get; }
    }
}
