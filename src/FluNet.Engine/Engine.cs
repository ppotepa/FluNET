using FluNET.Prompt;
using FluNET.TokenTree;
using TokenTreeClass = FluNET.TokenTree.TokenTree;

namespace FluNET
{
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;

        public Engine(TokenTreeFactory tokenTreeFactory)
        {
            this.tokenTreeFactory = tokenTreeFactory;
        }

        public TokenTreeClass? Run(ProcessedPrompt prompt)
        {
            var tree = tokenTreeFactory.Process(prompt);
            return tree;
        }
    }
}
