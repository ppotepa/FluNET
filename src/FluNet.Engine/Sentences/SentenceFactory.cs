using FluNET.Syntax.Core;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Words;

namespace FluNET.Sentences
{
    public class SentenceFactory(WordFactory wordFactory)
    {
        public ISentence? CreateFromTree(TokenTree tree)
        {
            // Debug: Print all tokens in the tree (only if console output is available)
            try
            {
                System.Diagnostics.Debug.WriteLine("  Tokens in tree:");
                int tokenIndex = 0;
                foreach (Token token in tree.GetTokens())
                {
                    System.Diagnostics.Debug.WriteLine($"    Token[{tokenIndex++}]: '{token.Value}' (Type: {token.Type})");
                }
            }
            catch (ObjectDisposedException)
            {
                // Console may be disposed in test scenarios - ignore
            }

            // Start from the ROOT token itself
            Token? current = tree.Root;
            if (current == null || current.Type == TokenType.Terminal)
            {
                return null;
            }

            // Build a linked chain of words from tokens
            IWord? firstWord = null;
            IWord? previousWord = null;

            while (current != null && current.Type != TokenType.Terminal)
            {
                IWord? word = wordFactory.CreateWord(current);
                if (word == null)
                {
                    // Skip unknown tokens or handle error
                    current = current.Next;
                    continue;
                }

                // Link the words together
                firstWord ??= word;

                if (previousWord != null)
                {
                    previousWord.Next = word;
                    word.Previous = previousWord;
                }

                previousWord = word;
                current = current.Next;
            }

            // Return a sentence wrapping the word chain
            // For now, we'll create a simple sentence implementation
            return new Sentence(firstWord);
        }
    }

    // Simple sentence implementation
    internal class Sentence : ISentence
    {
        public IWord? Root { get; }

        public Sentence(IWord? root)
        {
            Root = root;
        }
    }
}
