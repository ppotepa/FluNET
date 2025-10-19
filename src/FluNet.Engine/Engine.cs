using FluNET.Prompt;
using FluNET.Token.Tree;
using FluNET.Tokens;

namespace FluNET
{
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly DiscoveryService discovery;

        public Engine(TokenTreeFactory tokenTreeFactory, DiscoveryService discovery)
        {
            this.tokenTreeFactory = tokenTreeFactory;
            this.discovery = discovery;
        }

        public TokenTree Run(ProcessedPrompt prompt)
        {
            var tree = tokenTreeFactory.Process(prompt);

            // Discovery service now contains all available words (verbs, nouns)
            // Cached properties provide access to specific word types
            var availableVerbs = discovery.Verbs;
            var availableNouns = discovery.Nouns;
            var allWords = discovery.Words;

            return tree;
        }
    }
}
