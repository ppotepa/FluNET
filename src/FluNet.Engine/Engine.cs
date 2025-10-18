using FluNET.Prompt;
using FluNET.TokenTree;

namespace FluNET
{
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;

        public Engine(TokenTreeFactory tokenTreeFactory)
        {
            this.tokenTreeFactory = tokenTreeFactory;
        }

        public object? Run(ProcessedPrompt prompt)
        {
            var tree = tokenTreeFactory.Process(prompt);
            return default;
        }
    }
}
