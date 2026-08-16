using FluNET.Compilation;
using FluNET.Execution.Planning;
using FluNET.Execution.Steps;
using FluNET.Language.Binding;
using FluNET.Syntax.Validation;

namespace FluNET.Execution
{
    public class ExecutionPipelineFactory
    {
        private readonly SemanticCommandBinder _semanticBinder;
        private readonly SemanticProgramValidator _semanticValidator;
        private readonly TypedProgramCompiler _compiler;
        private readonly TypedProgramTypeValidator _typeValidator;
        private readonly ExecutionPlanner _planner;
        private readonly SentenceExecutor _executor;

        public ExecutionPipelineFactory(
            SemanticCommandBinder semanticBinder,
            TypedProgramCompiler compiler,
            TypedProgramTypeValidator typeValidator,
            ExecutionPlanner planner,
            SentenceExecutor executor)
        {
            _semanticBinder = semanticBinder ?? throw new ArgumentNullException(nameof(semanticBinder));
            _semanticValidator = new SemanticProgramValidator(_semanticBinder.Language);
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            _typeValidator = typeValidator ?? throw new ArgumentNullException(nameof(typeValidator));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public ExecutionPipeline CreateStandardPipeline()
        {
            ExecutionPipeline pipeline = new ExecutionPipeline()
                .AddStep(new ParsingStep())
                .AddStep(new SemanticBindingStep(_semanticBinder))
                .AddStep(new SemanticValidationStep(_semanticValidator));

            return pipeline
                .AddStep(new CommandCompilationStep(_compiler))
                .AddStep(new TypeValidationStep(_typeValidator))
                .AddStep(new PlanningStep(_planner))
                .AddStep(new PlanExecutionStep(_executor));
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
