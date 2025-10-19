using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax;
using FluNET.Token.Tree;
using FluNET.Tokens;

namespace FluNET
{
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly DiscoveryService discovery;
        private readonly SentenceFactory sentenceFactory;
        private readonly SentenceValidator sentenceValidator;

        public Engine(TokenTreeFactory tokenTreeFactory, SentenceFactory sentenceFactory,
            DiscoveryService discovery, SentenceValidator sentenceValidator)
        {
            this.tokenTreeFactory = tokenTreeFactory;
            this.sentenceFactory = sentenceFactory;
            this.discovery = discovery;
            this.sentenceValidator = sentenceValidator;
        }

        public TokenTree Run(ProcessedPrompt prompt)
        {
            var tree = tokenTreeFactory.Process(prompt);

            var validationResult = sentenceValidator.ValidateSentence(tree);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(
                    $"Invalid command: {validationResult.FailureReason}");
            }

            var sentence = sentenceFactory.CreateFromTree(tree);

            return tree;
        }
    }
}
