using FluNET.Tokens.Tree;

namespace FluNET.Execution.Steps
{
    /// <summary>
    /// Step 1: Tokenize the input prompt into a token tree
    /// </summary>
    public class TokenizationStep : IExecutionStep
    {
        private readonly TokenTreeFactory _tokenTreeFactory;

        public TokenizationStep(TokenTreeFactory tokenTreeFactory)
        {
            _tokenTreeFactory = tokenTreeFactory ?? throw new ArgumentNullException(nameof(tokenTreeFactory));
        }

        public ExecutionResult Execute(ExecutionContext context, Func<ExecutionContext, ExecutionResult> next)
        {
            try
            {
                // Process the prompt into a token tree
                context.TokenTree = _tokenTreeFactory.Process(context.Prompt);

                // Continue to next step
                return next(context);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                return ExecutionResult.Failed($"Tokenization failed: {ex.Message}");
            }
        }
    }
}
