using FluNET.Keywords;

namespace FluNET.Syntax.Nouns
{
    /// <summary>
    /// Represents a destination preposition - where something goes to.
    /// Example: In "SAVE data TO [output.txt]", [output.txt] implements ITo&lt;FileInfo&gt;.
    /// </summary>
    /// <typeparam name="TTo">The type of the destination</typeparam>
    public interface ITo<out TTo> : INoun, IKeyword
    {
        /// <summary>
        /// The destination where data is sent or saved.
        /// </summary>
        TTo To { get; }
    }
}
