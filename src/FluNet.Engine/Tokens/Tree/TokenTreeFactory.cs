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
            TokenTree tokenTree = new();

            IEnumerable<TokenClass> tokens = prompt.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(RawToken.Create)
                .Select(factory.CreateToken);

            foreach (TokenClass token in tokens)
            {
                tokenTree.AddToken(token);
            }

            return tokenTree;
        }
    }
}