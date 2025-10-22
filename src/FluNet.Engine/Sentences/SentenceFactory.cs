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

            // Split the token tree at THEN keywords to create sub-sentences
            List<List<Token>> tokenGroups = SplitAtThenKeywords(tree);

            if (tokenGroups.Count == 0)
            {
                return null;
            }

            // Create the main sentence from the first group
            ISentence? mainSentence = CreateSentenceFromTokens(tokenGroups[0]);
            if (mainSentence == null)
            {
                return null;
            }

            // Create sub-sentences from remaining groups
            for (int i = 1; i < tokenGroups.Count; i++)
            {
                ISentence? subSentence = CreateSentenceFromTokens(tokenGroups[i]);
                if (subSentence != null)
                {
                    mainSentence.SubSentences.Add(subSentence);
                }
            }

            return mainSentence;
        }

        /// <summary>
        /// Split token tree at THEN keywords to create separate sentence groups.
        /// </summary>
        private static List<List<Token>> SplitAtThenKeywords(TokenTree tree)
        {
            List<List<Token>> groups = [];
            List<Token> currentGroup = [];

            Token? current = tree.Root;
            while (current != null && current.Type != TokenType.Terminal)
            {
                // Check if this is a THEN keyword
                if (current.Type == TokenType.Regular && 
                    current.Value.Equals("THEN", StringComparison.OrdinalIgnoreCase))
                {
                    // Save current group and start a new one
                    if (currentGroup.Count > 0)
                    {
                        groups.Add(new List<Token>(currentGroup));
                        currentGroup.Clear();
                    }
                }
                else
                {
                    currentGroup.Add(current);
                }

                current = current.Next;
            }

            // Add the last group
            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        /// <summary>
        /// Create a sentence from a list of tokens.
        /// </summary>
        private ISentence? CreateSentenceFromTokens(List<Token> tokens)
        {
            if (tokens.Count == 0)
            {
                return null;
            }

            // Build a linked chain of words from tokens
            IWord? firstWord = null;
            IWord? previousWord = null;

            foreach (Token token in tokens)
            {
                IWord? word = wordFactory.CreateWord(token);
                if (word == null)
                {
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
            }

            return new Sentence(firstWord);
        }
    }

    // Simple sentence implementation
    internal class Sentence : ISentence
    {
        public IWord? Root { get; }
        public IList<ISentence> SubSentences { get; }
        public bool HasSubSentences => SubSentences.Count > 0;

        public Sentence(IWord? root)
        {
            Root = root;
            SubSentences = new List<ISentence>();
        }
    }
}