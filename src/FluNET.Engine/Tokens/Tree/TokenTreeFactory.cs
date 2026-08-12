using FluNET.Prompt;
using TokenClass = FluNET.Tokens.Token;

namespace FluNET.Tokens.Tree
{
    public class TokenTreeFactory
    {
        private readonly TokenFactory factory;

        public TokenTreeFactory(TokenFactory factory)
        {
            this.factory = factory;
        }

        public TokenTree Process(ProcessedPrompt prompt)
        {
            if (!prompt.IsValid)
            {
                string message = string.Join(" ", prompt.Diagnostics.Select(diagnostic => diagnostic.Message));
                throw new PromptSyntaxException(message, prompt.Diagnostics);
            }

            TokenTree tokenTree = new();

            // Use the pre-tokenized Tokens array from ProcessedPrompt
            // which respects brace boundaries {reference} and [variable]
            IEnumerable<TokenClass> tokens = prompt.Tokens
                .Select(RawToken.Create)
                .Select(factory.CreateToken)
                .Where(token => token.Type != TokenType.Terminal);

            foreach (TokenClass token in tokens)
            {
                tokenTree.AddToken(token);
            }

            return tokenTree;
        }
    }
}
