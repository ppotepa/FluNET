using FluNET.Execution.Steps;
using FluNET.Matching;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Variables;

namespace FluNET.Execution
{
    /// <summary>
    /// Factory for creating the standard execution pipeline.
    /// Allows customization and extension of the pipeline.
    /// </summary>
    public class ExecutionPipelineFactory
    {
        private readonly TokenTreeFactory _tokenTreeFactory;
        private readonly SentenceValidator _sentenceValidator;
        private readonly SentenceFactory _sentenceFactory;
        private readonly SentenceExecutor _sentenceExecutor;
        private readonly IVariableResolver _variableResolver;
        private readonly MatcherResolver _matcherResolver;

        public ExecutionPipelineFactory(
            TokenTreeFactory tokenTreeFactory,
            SentenceValidator sentenceValidator,
            SentenceFactory sentenceFactory,
            SentenceExecutor sentenceExecutor,
            IVariableResolver variableResolver,
            MatcherResolver matcherResolver)
        {
            _tokenTreeFactory = tokenTreeFactory ?? throw new ArgumentNullException(nameof(tokenTreeFactory));
            _sentenceValidator = sentenceValidator ?? throw new ArgumentNullException(nameof(sentenceValidator));
            _sentenceFactory = sentenceFactory ?? throw new ArgumentNullException(nameof(sentenceFactory));
            _sentenceExecutor = sentenceExecutor ?? throw new ArgumentNullException(nameof(sentenceExecutor));
            _variableResolver = variableResolver ?? throw new ArgumentNullException(nameof(variableResolver));
            _matcherResolver = matcherResolver ?? throw new ArgumentNullException(nameof(matcherResolver));
        }

        /// <summary>
        /// Creates the standard execution pipeline with all default steps
        /// </summary>
        public ExecutionPipeline CreateStandardPipeline()
        {
            var variableStorageStep = new VariableStorageStep(_variableResolver, _matcherResolver);

            return new ExecutionPipeline()
                .AddStep(new TokenizationStep(_tokenTreeFactory))
                .AddStep(new ValidationStep(_sentenceValidator))
                .AddStep(new SentenceCreationStep(_sentenceFactory))
                .AddStep(new SentenceExecutionStep(_sentenceExecutor))
                .AddStep(variableStorageStep)
                .AddStep(new SubSentenceExecutionStep(_sentenceExecutor, variableStorageStep));
        }

        /// <summary>
        /// Creates a custom pipeline with specified steps
        /// </summary>
        public ExecutionPipeline CreateCustomPipeline(Action<ExecutionPipeline> configurePipeline)
        {
            var pipeline = new ExecutionPipeline();
            configurePipeline(pipeline);
            return pipeline;
        }
    }
}
