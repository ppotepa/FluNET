using FluNET.Tokens;

namespace FluNET.Sentences
{
    public class SentenceFactory
    {
        private readonly DiscoveryService discovery;

        public SentenceFactory(DiscoveryService discovery)
        {
            this.discovery = discovery;
        }

        public ISentence CreateFromTree(TokenTree tree)
        {
            return default;
        }
    }
}
