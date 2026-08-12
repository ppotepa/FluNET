using FluNET.Execution.Planning;
using FluNET.Execution.Steps;
using FluNET.Language.Binding;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;

namespace FluNET.Execution
{
    /// <summary>
    /// Creates standard and custom execution pipelines. The standard pipeline
    /// uses parsed syntax, semantic binding, frame validation, planning, and
    /// typed command execution. Legacy token/sentence services are retained in
    /// the constructor only for public API compatibility.
    /// </summary>
    public class ExecutionPipelineFactory
    {
        private readonly SemanticCommandBinder _semanticBinder;
        private readonly SemanticProgramValidator _semanticValidator;
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
            ArgumentNullException.ThrowIfNull(tokenTreeFactory);
            ArgumentNullException.ThrowIfNull(sentenceValidator);
            ArgumentNullException.ThrowIfNull(sentenceFactory);
            _semanticBinder = semanticBinder ?? throw new ArgumentNullException(nameof(semanticBinder));
            _semanticValidator = new SemanticProgramValidator(_semanticBinder.Language);
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _planExecutor = planExecutor ?? throw new ArgumentNullException(nameof(planExecutor));
        }

        /// <summary>
        /// Creates the canonical Parse, Bind, Validate, Plan, Execute pipeline.
        /// It does not instantiate legacy words, token trees, or sentences.
        /// </summary>
        public ExecutionPipeline CreateStandardPipeline()
        {
            return new ExecutionPipeline()
                .AddStep(new ParsingStep())
                .AddStep(new SemanticBindingStep(_semanticBinder))
                .AddStep(new SemanticValidationStep(_semanticValidator))
                .AddStep(new PlanningStep(_planner))
                .AddStep(new PlanExecutionStep(_planExecutor));
        }

        /// <summary>Creates an empty pipeline and applies custom steps to it.</summary>
        public ExecutionPipeline CreateCustomPipeline(Action<ExecutionPipeline> configurePipeline)
        {
            ArgumentNullException.ThrowIfNull(configurePipeline);
            var pipeline = new ExecutionPipeline();
            configurePipeline(pipeline);
            return pipeline;
        }
    }
}
