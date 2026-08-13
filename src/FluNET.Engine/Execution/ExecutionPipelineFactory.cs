using FluNET.Compilation;
using FluNET.Execution.Planning;
using FluNET.Execution.Steps;
using FluNET.Language.Binding;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;

namespace FluNET.Execution
{
    public class ExecutionPipelineFactory
    {
        private readonly SemanticCommandBinder _semanticBinder;
        private readonly SemanticProgramValidator _semanticValidator;
        private readonly TypedProgramCompiler? _compiler;
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

        public ExecutionPipelineFactory(
            TokenTreeFactory tokenTreeFactory,
            SentenceValidator sentenceValidator,
            SentenceFactory sentenceFactory,
            SemanticCommandBinder semanticBinder,
            TypedProgramCompiler compiler,
            ExecutionPlanner planner,
            ExecutionPlanExecutor planExecutor)
            : this(
                tokenTreeFactory,
                sentenceValidator,
                sentenceFactory,
                semanticBinder,
                planner,
                planExecutor)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        }

        public ExecutionPipeline CreateStandardPipeline()
        {
            ExecutionPipeline pipeline = new ExecutionPipeline()
                .AddStep(new ParsingStep())
                .AddStep(new SemanticBindingStep(_semanticBinder))
                .AddStep(new SemanticValidationStep(_semanticValidator));

            if (_compiler is not null)
            {
                pipeline
                    .AddStep(new CommandCompilationStep(_compiler))
                    .AddStep(new TypeValidationStep());
            }

            return pipeline
                .AddStep(new PlanningStep(_planner))
                .AddStep(new PlanExecutionStep(_planExecutor));
        }

        public ExecutionPipeline CreateCustomPipeline(Action<ExecutionPipeline> configurePipeline)
        {
            ArgumentNullException.ThrowIfNull(configurePipeline);
            var pipeline = new ExecutionPipeline();
            configurePipeline(pipeline);
            return pipeline;
        }
    }
}
