using FluNET.Prompt;
using FluNET.Tokens;
using TokenClass = FluNET.Tokens.Token;

namespace FluNET.TokenTree
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
            var tokenTree = new TokenTree();

            var tokens = prompt.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(RawToken.Create)
                .Select(factory.CreateToken);

            foreach (var token in tokens)
            {
                tokenTree.AddToken(token);
            }

            return tokenTree;
        }
    }
}