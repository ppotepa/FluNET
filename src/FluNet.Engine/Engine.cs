using FluNET.Prompt;
using FluNET.Token.Tree;
using FluNET.Tokens;

namespace FluNET
{
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly DiscoveryService discovery;

        public Engine(TokenTreeFactory tokenTreeFactory)
        {
            this.tokenTreeFactory = tokenTreeFactory;
        }

        public TokenTree Run(ProcessedPrompt prompt)
        {
            var tree = tokenTreeFactory.Process(prompt);
            return tree;
        }
    }
}
