using FluNET.Prompt;
using FluNET.Token;

namespace FluNET.TokenTree
{
    public class TokenTreeFactory

    {
        private readonly TokenFactory factory;

        public TokenTreeFactory(TokenFactory factory)
        {
            this.factory = factory;
        }

        internal object Process(ProcessedPrompt prompt)
        {
            var tokens = prompt.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(RawToken.Create)
                .Select(factory.Process);

            return factory.Process(tokens);
        }
    }
}