using FluNET.Compilation;
using FluNET.Execution;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;

namespace FluNET
{
    /// <summary>
    /// Main execution engine for FluNET natural language commands.
    /// Now uses a pipeline architecture for better modularity and extensibility.
    /// </summary>
    public class Engine
    {
        private readonly ExecutionPipelineFactory _pipelineFactory;
        private readonly IVariableResolver variableResolver;

        // Keep old dependencies for backward compatibility
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly SentenceFactory sentenceFactory;
        private readonly SentenceValidator sentenceValidator;
        private readonly SemanticCommandBinder semanticBinder;
        private readonly SemanticProgramValidator semanticValidator;
        private readonly ExecutionPlanner executionPlanner;
        private readonly LanguageSnapshot language;

        public Engine(TokenTreeFactory tokenTreeFactory, SentenceFactory sentenceFactory,
            SentenceValidator sentenceValidator, IVariableResolver variableResolver,
            ExecutionPipelineFactory pipelineFactory,
            SemanticCommandBinder semanticBinder,
            ExecutionPlanner executionPlanner,
            LanguageSnapshot language)
        {
            this.tokenTreeFactory = tokenTreeFactory;
            this.sentenceFactory = sentenceFactory;
            this.sentenceValidator = sentenceValidator;
            this.variableResolver = variableResolver;
            this.semanticBinder = semanticBinder ?? throw new ArgumentNullException(nameof(semanticBinder));
            this.executionPlanner = executionPlanner ?? throw new ArgumentNullException(nameof(executionPlanner));
            this.language = language ?? throw new ArgumentNullException(nameof(language));
            semanticValidator = new SemanticProgramValidator(this.language);
            _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
        }

        /// <summary>
        /// Register a variable that can be used in sentences.
        /// Variables can be referenced using [VariableName] syntax.
        /// </summary>
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable (case-insensitive)</param>
        /// <param name="value">The value of the variable</param>
        public void RegisterVariable<T>(string name, T value)
        {
            variableResolver.Register(name, value);
        }

        /// <summary>
        /// Parses, binds, validates, and plans a prompt without executing it or
        /// performing external effects. The result exposes the canonical parsed
        /// and bound program together with source-aware compiler diagnostics.
        /// </summary>
        public CompilationResult Analyze(ProcessedPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            prompt = prompt.WithGrammar(language.Grammar);

            FluNetProgram program = new(prompt);
            DiagnosticBag diagnostics = new();
            AddPromptDiagnostics(diagnostics, prompt, prompt.Diagnostics);

            // Parse
            if (!prompt.IsValid)
            {
                string reason = string.Join(" ", prompt.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(reason),
                    null,
                    diagnostics,
                    null,
                    null,
                    CompilationPhase.Parse);
            }

            IReadOnlyList<TokenTree> commandTrees;
            try
            {
                // Compatibility token trees are still required by the legacy
                // validator until Batch 5 removes them from the canonical path.
                commandTrees = tokenTreeFactory.ProcessCommands(prompt);
            }
            catch (PromptSyntaxException exception)
            {
                AddPromptDiagnostics(diagnostics, prompt, exception.Diagnostics);
                if (exception.Diagnostics.Count == 0)
                {
                    diagnostics.Add(
                        CompilationDiagnosticCodes.ParseFailure,
                        CompilationPhase.Parse,
                        exception.Message,
                        prompt.Syntax.Span);
                }

                return new CompilationResult(
                    program,
                    ValidationResult.Failure(exception.Message),
                    null,
                    diagnostics,
                    null,
                    null,
                    CompilationPhase.Parse);
            }

            // Bind
            BoundProgram boundProgram;
            try
            {
                IReadOnlyList<BoundCommand> boundCommands = semanticBinder.BindProgram(prompt.Syntax);
                boundProgram = BoundProgram.FromCommands(program, boundCommands);
            }
            catch (SemanticBindingException exception)
            {
                diagnostics.Add(
                    CompilationDiagnosticCodes.BindingFailure,
                    CompilationPhase.Bind,
                    exception.Message,
                    exception.Span);
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(exception.Message),
                    null,
                    diagnostics,
                    null,
                    null,
                    CompilationPhase.Bind);
            }

            // Validate using only the language snapshot, frame, and slots.
            DiagnosticBag semanticDiagnostics = semanticValidator.Validate(boundProgram);
            diagnostics.AddRange(semanticDiagnostics);
            if (semanticDiagnostics.HasErrors)
            {
                string reason = string.Join(" ", semanticDiagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(reason),
                    null,
                    diagnostics,
                    boundProgram,
                    null,
                    CompilationPhase.Validate);
            }

            // Compatibility validation remains until Batch 5 removes the legacy
            // sentence path from standard execution.
            ValidationResult validation = sentenceValidator.ValidateCommands(commandTrees);
            if (!validation.IsValid)
            {
                string reason = validation.FailureReason ?? "Legacy compatibility validation failed.";
                diagnostics.Add(
                    CompilationDiagnosticCodes.ValidationFailure,
                    CompilationPhase.Validate,
                    reason,
                    prompt.Syntax.Span);
                return new CompilationResult(
                    program,
                    validation,
                    null,
                    diagnostics,
                    boundProgram,
                    null,
                    CompilationPhase.Validate);
            }

            ISentence? sentence = sentenceFactory.CreateFromTrees(commandTrees);
            if (sentence is null)
            {
                const string reason = "Could not create a compatibility sentence from the prompt.";
                diagnostics.Add(
                    CompilationDiagnosticCodes.CompatibilitySentenceFailure,
                    CompilationPhase.Validate,
                    reason,
                    prompt.Syntax.Span);
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(reason),
                    null,
                    diagnostics,
                    boundProgram,
                    null,
                    CompilationPhase.Validate);
            }

            // Plan
            try
            {
                ExecutionPlan plan = executionPlanner.Create(boundProgram.Commands, prompt.Syntax);
                return new CompilationResult(
                    program,
                    validation,
                    sentence,
                    diagnostics,
                    boundProgram,
                    plan,
                    null);
            }
            catch (ExecutionPlanException exception)
            {
                diagnostics.Add(
                    CompilationDiagnosticCodes.PlanningFailure,
                    CompilationPhase.Plan,
                    exception.Message,
                    prompt.Syntax.Span);
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(exception.Message),
                    sentence,
                    diagnostics,
                    boundProgram,
                    null,
                    CompilationPhase.Plan);
            }
        }

        /// <summary>
        /// Parse, validate, and execute a sentence using the execution pipeline.
        /// Supports THEN clause for chaining multiple commands with shared variable context.
        /// Example: DOWNLOAD [file] FROM http://example.com TO {file.txt} THEN SAY [file].
        /// </summary>
        /// <param name="prompt">The prompt to process</param>
        /// <returns>A tuple containing validation result, the sentence, and execution result</returns>
        public (ValidationResult ValidationResult, ISentence? Sentence, object? Result) Run(ProcessedPrompt prompt)
        {
            ExecutionResult result = Execute(prompt);
            ValidationResult compatibilityValidation = result.IsSuccess
                ? result.ValidationResult
                : ValidationResult.Failure(result.Error?.Message ?? result.ValidationResult.FailureReason ?? "Execution failed.");
            return (compatibilityValidation, result.Sentence, result.Result);
        }

        /// <summary>Runs a prompt and returns structured validation or execution errors.</summary>
        public ExecutionResult Execute(ProcessedPrompt prompt)
        {
            return ExecuteAsync(prompt).GetAwaiter().GetResult();
        }

        /// <summary>Asynchronously runs a prompt with cancellation support.</summary>
        public async Task<ExecutionResult> ExecuteAsync(
            ProcessedPrompt prompt,
            CancellationToken cancellationToken = default)
            => await ExecuteAsync(
                prompt,
                new WorkflowExecutionOptions(),
                cancellationToken).ConfigureAwait(false);

        /// <summary>Runs or resumes a workflow with an explicit stable run identifier.</summary>
        public async Task<ExecutionResult> ExecuteAsync(
            ProcessedPrompt prompt,
            WorkflowExecutionOptions workflowOptions,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            ArgumentNullException.ThrowIfNull(workflowOptions);
            prompt = prompt.WithGrammar(language.Grammar);
            ExecutionPipeline pipeline = _pipelineFactory.CreateStandardPipeline();
            var context = new Execution.ExecutionContext(prompt, workflowOptions);
            return await pipeline.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute with a custom pipeline configuration.
        /// Allows advanced scenarios with custom execution steps.
        /// </summary>
        public (ValidationResult ValidationResult, ISentence? Sentence, object? Result) RunWithCustomPipeline(
            ProcessedPrompt prompt,
            Action<ExecutionPipeline> configurePipeline)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            prompt = prompt.WithGrammar(language.Grammar);
            var pipeline = _pipelineFactory.CreateCustomPipeline(configurePipeline);
            var context = new Execution.ExecutionContext(prompt);
            var result = pipeline.Execute(context);
            ValidationResult compatibilityValidation = result.IsSuccess
                ? result.ValidationResult
                : ValidationResult.Failure(result.Error?.Message ?? result.ValidationResult.FailureReason ?? "Execution failed.");
            return (compatibilityValidation, result.Sentence, result.Result);
        }

        private static void AddPromptDiagnostics(
            DiagnosticBag diagnostics,
            ProcessedPrompt prompt,
            IEnumerable<PromptDiagnostic> promptDiagnostics)
        {
            foreach (PromptDiagnostic diagnostic in promptDiagnostics)
            {
                int start = Math.Clamp(diagnostic.Position, 0, prompt.SourceText.Length);
                int length = start < prompt.SourceText.Length ? 1 : 0;
                diagnostics.Add(
                    diagnostic.Code,
                    CompilationPhase.Parse,
                    diagnostic.Message,
                    new SourceSpan(start, length));
            }
        }
    }
}
