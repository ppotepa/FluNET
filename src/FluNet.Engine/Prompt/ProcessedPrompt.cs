using System.Text;

namespace FluNET.Prompt
{
    public class ProcessedPrompt
    {
        private readonly string prompt;

        public ProcessedPrompt(string prompt)
        {
            this.prompt = prompt;
            Tokens = TokenizeWithBraceAwareness(prompt);
        }

        public string[] Tokens { get; }

        /// <summary>
        /// Tokenizes the prompt with awareness of brace patterns {reference} and [variable].
        /// Spaces inside braces or brackets are preserved as part of the token.
        /// Supports nested braces like {{{value}}}.
        /// </summary>
        private static string[] TokenizeWithBraceAwareness(string input)
        {
            List<string> tokens = [];
            StringBuilder currentToken = new();
            int braceDepth = 0;  // Track nested braces
            int bracketDepth = 0;  // Track nested brackets

            foreach (char c in input)
            {
                // Track brace/bracket depth for nesting support
                if (c == '{')
                {
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;
                }
                else if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']')
                {
                    bracketDepth--;
                }

                // If we hit a space and we're not inside braces/brackets
                if (c == ' ' && braceDepth == 0 && bracketDepth == 0)
                {
                    // Add the completed token if not empty
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                }
                else
                {
                    // Add the character to the current token
                    currentToken.Append(c);
                }
            }

            // Add the final token if not empty
            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens.ToArray();
        }

        public override string ToString()
        {
            return prompt.Replace("  ", " ").Trim();
        }
    }
}