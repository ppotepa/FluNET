using FluNET.Execution.Steps;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Language.Binding;
using FluNET.Execution.Planning;

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
        private readonly SemanticCommandBinder _semanticBinder;
        private readonly ExecutionPlanner _planner;
        private readonly ExecutionPlanExecutor _planExecutor;

        public ExecutionPipelineFactory(
            TokenTreeFactory tokenTreeFactory,
            SentenceValidator sentenceValidator,
            SentenceFactory sentenceFactory,
            SemanticCommandBinder semanticBinder,
            ExecutionPlanner planner,
            ExecutionPlanExecutor planExecutor)
        {
            _tokenTreeFactory = tokenTreeFactory ?? throw new ArgumentNullException(nameof(tokenTreeFactory));
            _sentenceValidator = sentenceValidator ?? throw new ArgumentNullException(nameof(sentenceValidator));
            _sentenceFactory = sentenceFactory ?? throw new ArgumentNullException(nameof(sentenceFactory));
            _semanticBinder = semanticBinder ?? throw new ArgumentNullException(nameof(semanticBinder));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _planExecutor = planExecutor ?? throw new ArgumentNullException(nameof(planExecutor));
        }

        /// <summary>
        /// Creates the standard execution pipeline with all default steps
        /// </summary>
        public ExecutionPipeline CreateStandardPipeline()
        {
            return new ExecutionPipeline()
                .AddStep(new TokenizationStep(_tokenTreeFactory))
                .AddStep(new SemanticBindingStep(_semanticBinder))
                .AddStep(new ValidationStep(_sentenceValidator))
                .AddStep(new SentenceCreationStep(_sentenceFactory))
                .AddStep(new PlanningStep(_planner))
                .AddStep(new PlanExecutionStep(_planExecutor));
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
