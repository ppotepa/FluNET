namespace FluNET.Syntax.Core
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

    /// <summary>
    /// Extension methods for IWord navigation
    /// </summary>
    public static class IWordExtensions
    {
        /// <summary>
        /// Find the next word of the specified type in the chain
        /// </summary>
        public static T? FindNext<T>(this IWord? word) where T : class, IWord
        {
            IWord? current = word?.Next;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = current.Next;
            }
            return null;
        }

        /// <summary>
        /// Find the previous word of the specified type in the chain
        /// </summary>
        public static T? FindPrevious<T>(this IWord? word) where T : class, IWord
        {
            IWord? current = word?.Previous;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = current.Previous;
            }
            return null;
        }

        /// <summary>
        /// Find a word of the specified type anywhere in the chain (forward first, then backward)
        /// </summary>
        public static T? Find<T>(this IWord? word) where T : class, IWord
        {
            // Try to find forward first
            T? forward = word.FindNext<T>();
            if (forward != null)
            {
                return forward;
            }

            // Then try backward
            return word.FindPrevious<T>();
        }
    }
}