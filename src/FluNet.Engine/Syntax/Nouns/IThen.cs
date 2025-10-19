using FluNET.Keywords;

namespace FluNET.Syntax
{
    /// <summary>
    /// Represents a continuation of a sentence that operates on the same data.
    /// Allows chaining multiple operations on the same data source.
    /// Example: GET [data] FROM source THEN SAVE TO file.
    /// </summary>
    /// <typeparam name="TData">The type of data being processed through the chain</typeparam>
    public interface IThen<TData> : INoun, IKeyword
    {
        /// <summary>
        /// Gets the data that is being passed to the next operation in the chain.
        /// This is the result from the previous verb's execution.
        /// </summary>
        TData Data { get; }
    }
}
