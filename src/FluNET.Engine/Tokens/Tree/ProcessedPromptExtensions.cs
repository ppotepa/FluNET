using FluNET.Prompt;

namespace FluNET.Tokens.Tree
{
    public static class ProcessedPromptExtensions
    {
        /// <summary>
        /// Extension method to convert a ProcessedPrompt to a TokenTree
        /// </summary>
        /// <param name="prompt">The processed prompt to convert</param>
        /// <param name="tokenFactory">Optional token factory. If null, a new one will be created.</param>
        /// <returns>A TokenTree containing the tokenized prompt</returns>
        public static TokenTree ToTokenTree(this ProcessedPrompt prompt, TokenFactory? tokenFactory = null)
        {
            TokenFactory factory = tokenFactory ?? new TokenFactory();
            TokenTreeFactory tokenTreeFactory = new(factory);
            return tokenTreeFactory.Process(prompt);
        }
    }
}