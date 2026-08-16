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
<<<<<<< HEAD
            ArgumentNullException.ThrowIfNull(prompt);
=======
>>>>>>> origin/agent/stabilize-poc-foundation
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
<<<<<<< HEAD

        /// <summary>
        /// Creates one compatibility token tree for one already parsed command.
        /// Command boundaries are owned by <see cref="PromptSyntax"/> and are not
        /// rediscovered from token text.
        /// </summary>
        public TokenTree Process(CommandSyntax command)
        {
            ArgumentNullException.ThrowIfNull(command);
            TokenTree tokenTree = new();

            foreach (PromptToken syntaxToken in command.Tokens)
            {
                TokenClass token = factory.CreateToken(RawToken.Create(syntaxToken.Text));
                if (token.Type != TokenType.Terminal)
                {
                    tokenTree.AddToken(token);
                }
            }

            return tokenTree;
        }

        /// <summary>Adapts the canonical syntax tree to one legacy tree per command.</summary>
        public IReadOnlyList<TokenTree> ProcessCommands(ProcessedPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            if (!prompt.IsValid)
            {
                string message = string.Join(" ", prompt.Diagnostics.Select(diagnostic => diagnostic.Message));
                throw new PromptSyntaxException(message, prompt.Diagnostics);
            }

            return prompt.Syntax.Commands.Select(Process).ToArray();
        }
=======
>>>>>>> origin/agent/stabilize-poc-foundation
    }
}
