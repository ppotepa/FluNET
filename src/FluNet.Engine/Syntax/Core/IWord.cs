namespace FluNET.Syntax
{
    /// <summary>
    /// Base interface for all words in the language.
    /// Provides navigation similar to Token for traversing word chains.
    /// </summary>
    public interface IWord
    {
        IWord? Next { get; set; }
        IWord? Previous { get; set; }
    }
}
