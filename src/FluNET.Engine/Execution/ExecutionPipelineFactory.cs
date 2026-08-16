<<<<<<< HEAD
using FluNET.Compilation;
using FluNET.Execution.Planning;
using FluNET.Execution.Steps;
using FluNET.Language.Binding;
using FluNET.Language.Values;
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
        private readonly TypedProgramTypeValidator? _typeValidator;
        private readonly ExecutionPlanner _planner;
        private readonly ExecutionPlanExecutor _planExecutor;
=======
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
>>>>>>> origin/agent/stabilize-poc-foundation

        public ExecutionPipelineFactory(
            TokenTreeFactory tokenTreeFactory,
            SentenceValidator sentenceValidator,
            SentenceFactory sentenceFactory,
<<<<<<< HEAD
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

        /// <summary>Compatibility constructor retained for pre-validator hosts.</summary>
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
            _typeValidator = new TypedProgramTypeValidator(
                _semanticBinder.Language,
                ValueCodecRegistryFactory.CreateDefault(_semanticBinder.Language));
        }

        /// <summary>Canonical 0.4 constructor with host variables and conversion-aware type validation.</summary>
        public ExecutionPipelineFactory(
            TokenTreeFactory tokenTreeFactory,
            SentenceValidator sentenceValidator,
            SentenceFactory sentenceFactory,
            SemanticCommandBinder semanticBinder,
            TypedProgramCompiler compiler,
            TypedProgramTypeValidator typeValidator,
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
            _typeValidator = typeValidator ?? throw new ArgumentNullException(nameof(typeValidator));
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
                    .AddStep(new TypeValidationStep(
                        _typeValidator ?? new TypedProgramTypeValidator()));
            }

            return pipeline
                .AddStep(new PlanningStep(_planner))
                .AddStep(new PlanExecutionStep(_planExecutor));
        }

        public ExecutionPipeline CreateCustomPipeline(Action<ExecutionPipeline> configurePipeline)
        {
            ArgumentNullException.ThrowIfNull(configurePipeline);
=======
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
>>>>>>> origin/agent/stabilize-poc-foundation
            var pipeline = new ExecutionPipeline();
            configurePipeline(pipeline);
            return pipeline;
        }
    }
}
