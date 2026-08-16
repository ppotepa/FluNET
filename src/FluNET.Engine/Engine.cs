<<<<<<< HEAD
using FluNET.Compatibility;
using FluNET.Compilation;
using FluNET.Execution;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using FluNET.Language;
using FluNET.Language.Binding;
=======
﻿using FluNET.Execution;
using FluNET.Matching;
>>>>>>> origin/agent/stabilize-poc-foundation
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Variables;
<<<<<<< HEAD
using Microsoft.Extensions.DependencyInjection;

namespace FluNET
{
    /// <summary>Main compilation and execution entry point for FluNET programs.</summary>
=======
using FluNET.Words;
using System.Text.Json;

namespace FluNET
{
    /// <summary>
    /// Main execution engine for FluNET natural language commands.
    /// Now uses a pipeline architecture for better modularity and extensibility.
    /// </summary>
>>>>>>> origin/agent/stabilize-poc-foundation
    public class Engine
    {
        private readonly ExecutionPipelineFactory _pipelineFactory;
        private readonly IVariableResolver variableResolver;
<<<<<<< HEAD
        private readonly SemanticCommandBinder semanticBinder;
        private readonly SemanticProgramValidator semanticValidator;
        private readonly ExecutionPlanner executionPlanner;
        private readonly LanguageSnapshot language;
        private readonly LegacySentenceAdapter legacySentenceAdapter;

        /// <summary>Creates the canonical engine with an isolated legacy compatibility adapter.</summary>
        [ActivatorUtilitiesConstructor]
        public Engine(
            IVariableResolver variableResolver,
            ExecutionPipelineFactory pipelineFactory,
            SemanticCommandBinder semanticBinder,
            ExecutionPlanner executionPlanner,
            LanguageSnapshot language,
            LegacySentenceAdapter legacySentenceAdapter)
        {
            this.variableResolver = variableResolver ?? throw new ArgumentNullException(nameof(variableResolver));
            _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
            this.semanticBinder = semanticBinder ?? throw new ArgumentNullException(nameof(semanticBinder));
            this.executionPlanner = executionPlanner ?? throw new ArgumentNullException(nameof(executionPlanner));
            this.language = language ?? throw new ArgumentNullException(nameof(language));
            this.legacySentenceAdapter = legacySentenceAdapter ?? throw new ArgumentNullException(nameof(legacySentenceAdapter));
            semanticValidator = new SemanticProgramValidator(this.language);
        }

        /// <summary>Compatibility constructor for hosts that still assemble the pre-0.3 sentence services manually.</summary>
        [Obsolete("Use the canonical Engine constructor or FluNETContext. Legacy sentence services are isolated behind LegacySentenceAdapter.")]
        public Engine(
            TokenTreeFactory tokenTreeFactory,
            SentenceFactory sentenceFactory,
            SentenceValidator sentenceValidator,
            IVariableResolver variableResolver,
            ExecutionPipelineFactory pipelineFactory,
            SemanticCommandBinder semanticBinder,
            ExecutionPlanner executionPlanner,
            LanguageSnapshot language)
            : this(
                variableResolver,
                pipelineFactory,
                semanticBinder,
                executionPlanner,
                language,
                new LegacySentenceAdapter(
                    tokenTreeFactory,
                    sentenceValidator,
                    sentenceFactory,
                    language))
        {
=======

        // Keep old dependencies for backward compatibility
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly SentenceFactory sentenceFactory;
        private readonly SentenceValidator sentenceValidator;
        private readonly SentenceExecutor sentenceExecutor;
        private readonly MatcherResolver matcherResolver;

        public Engine(TokenTreeFactory tokenTreeFactory, SentenceFactory sentenceFactory,
            SentenceValidator sentenceValidator, IVariableResolver variableResolver,
            SentenceExecutor sentenceExecutor, MatcherResolver matcherResolver,
            ExecutionPipelineFactory pipelineFactory)
        {
            this.tokenTreeFactory = tokenTreeFactory;
            this.sentenceFactory = sentenceFactory;
            this.sentenceValidator = sentenceValidator;
            this.variableResolver = variableResolver;
            this.sentenceExecutor = sentenceExecutor;
            this.matcherResolver = matcherResolver;
            _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
>>>>>>> origin/agent/stabilize-poc-foundation
        }

        /// <summary>
        /// Register a variable that can be used in sentences.
        /// Variables can be referenced using [VariableName] syntax.
        /// </summary>
<<<<<<< HEAD
=======
        /// <typeparam name="T">The type of the variable</typeparam>
        /// <param name="name">The name of the variable (case-insensitive)</param>
        /// <param name="value">The value of the variable</param>
>>>>>>> origin/agent/stabilize-poc-foundation
        public void RegisterVariable<T>(string name, T value)
        {
            variableResolver.Register(name, value);
        }

        /// <summary>
<<<<<<< HEAD
        /// Parses, binds, validates, and plans a prompt without executing it or
        /// performing external effects. Legacy sentence projection is optional
        /// compatibility metadata and never determines compilation success.
        /// </summary>
        public CompilationResult Analyze(ProcessedPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            prompt = prompt.WithGrammar(language.Grammar);

            FluNetProgram program = new(prompt);
            DiagnosticBag diagnostics = new();
            AddPromptDiagnostics(diagnostics, prompt, prompt.Diagnostics);
=======
        /// Parses and validates a prompt without executing it or performing external effects.
        /// </summary>
        public PromptAnalysis Analyze(ProcessedPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
>>>>>>> origin/agent/stabilize-poc-foundation

            if (!prompt.IsValid)
            {
                string reason = string.Join(" ", prompt.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
<<<<<<< HEAD
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(reason),
                    null,
                    diagnostics,
                    null,
                    null,
                    CompilationPhase.Parse);
            }

            if (prompt.Syntax.Commands.Count == 0)
            {
                const string reason = "Empty prompt does not contain a command.";
                diagnostics.Add(
                    CompilationDiagnosticCodes.EmptyProgram,
                    CompilationPhase.Parse,
                    reason,
                    prompt.Syntax.Span);
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(reason),
                    null,
                    diagnostics,
                    null,
                    null,
                    CompilationPhase.Parse);
            }

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
=======
                return new PromptAnalysis(prompt, ValidationResult.Failure(reason), null);
>>>>>>> origin/agent/stabilize-poc-foundation
            }

            try
            {
<<<<<<< HEAD
                string? parallelConflict = FindParallelWriteConflict(boundProgram, prompt.Syntax);
                if (parallelConflict is not null)
                {
                    return PlanningFailure(program, boundProgram, diagnostics, prompt, parallelConflict);
                }
                ExecutionPlan plan = executionPlanner.Create(boundProgram.Commands, prompt.Syntax);
                ISentence? compatibilitySentence = null;
                if (boundProgram.Commands.All(command => command.Frame.HasLegacyVerbAdapter))
                {
                    LegacySentenceAdaptation adaptation = legacySentenceAdapter.Adapt(prompt);
                    compatibilitySentence = adaptation.IsValid ? adaptation.Sentence : null;
                }

                return new CompilationResult(
                    program,
                    ValidationResult.Success(),
                    compatibilitySentence,
                    diagnostics,
                    boundProgram,
                    plan,
                    null);
            }
            catch (ExecutionPlanException exception)
            {
                return PlanningFailure(program, boundProgram, diagnostics, prompt, exception.Message);
            }
            catch (Exception exception) when (
                exception is FormatException or NotSupportedException or InvalidOperationException)
            {
                return PlanningFailure(
                    program,
                    boundProgram,
                    diagnostics,
                    prompt,
                    $"Invalid execution policy expression: {exception.Message}");
            }
        }

        private static CompilationResult PlanningFailure(
            FluNetProgram program,
            BoundProgram boundProgram,
            DiagnosticBag diagnostics,
            ProcessedPrompt prompt,
            string message)
        {
            diagnostics.Add(
                CompilationDiagnosticCodes.PlanningFailure,
                CompilationPhase.Plan,
                message,
                prompt.Syntax.Span);
            return new CompilationResult(
                program,
                ValidationResult.Failure(message),
                null,
                diagnostics,
                boundProgram,
                null,
                CompilationPhase.Plan);
        }

        private static string? FindParallelWriteConflict(
            BoundProgram program,
            PromptSyntax syntax)
        {
            foreach (CommandLinkSyntax link in syntax.Links.Where(link =>
                link.Kind == CommandLinkKind.Parallel))
            {
                if (link.PredecessorIndex >= program.Commands.Count ||
                    link.SuccessorIndex >= program.Commands.Count)
                {
                    continue;
                }

                HashSet<string> left = OutputNames(program.Commands[link.PredecessorIndex]);
                foreach (string name in OutputNames(program.Commands[link.SuccessorIndex]))
                {
                    if (left.Contains(name))
                    {
                        return $"Parallel commands {link.PredecessorIndex} and " +
                            $"{link.SuccessorIndex} both write [{name}].";
                    }
                }
            }
            return null;
        }

        private static HashSet<string> OutputNames(BoundCommand command) =>
            command.Arguments.Values
                .Where(argument => argument.Slot.Direction == SlotDirection.Output)
                .SelectMany(argument => argument.Tokens)
                .Where(token => token.Kind == PromptTokenKind.Variable)
                .Select(token => token.Text[1..^1].ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Legacy synchronous API. It first validates/projects ISentence through
        /// LegacySentenceAdapter and then executes the same canonical pipeline as ExecuteAsync.
        /// </summary>
        [Obsolete("Use Analyze plus Execute/ExecuteAsync. ISentence is a compatibility projection and is not part of canonical execution.")]
        public (ValidationResult ValidationResult, ISentence? Sentence, object? Result) Run(ProcessedPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            LegacySentenceAdaptation compatibility = legacySentenceAdapter.Adapt(prompt);
            if (!compatibility.IsValid)
            {
                return (compatibility.ValidationResult, null, null);
            }

            ExecutionResult result = Execute(prompt);
            ValidationResult compatibilityValidation = result.IsSuccess
                ? compatibility.ValidationResult
                : ValidationResult.Failure(
                    result.Error?.Message ??
                    result.ValidationResult.FailureReason ??
                    "Execution failed.");
            return (compatibilityValidation, compatibility.Sentence, result.Result);
=======
                TokenTree tree = tokenTreeFactory.Process(prompt);
                ValidationResult validation = sentenceValidator.ValidateSentence(tree);
                ISentence? sentence = validation.IsValid ? sentenceFactory.CreateFromTree(tree) : null;

                if (validation.IsValid && sentence is null)
                {
                    validation = ValidationResult.Failure("Could not create a sentence from the prompt.");
                }

                return new PromptAnalysis(prompt, validation, sentence);
            }
            catch (PromptSyntaxException exception)
            {
                return new PromptAnalysis(prompt, ValidationResult.Failure(exception.Message), null);
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
>>>>>>> origin/agent/stabilize-poc-foundation
        }

        /// <summary>Runs a prompt and returns structured validation or execution errors.</summary>
        public ExecutionResult Execute(ProcessedPrompt prompt)
        {
            return ExecuteAsync(prompt).GetAwaiter().GetResult();
        }

<<<<<<< HEAD
        /// <summary>
        /// Asynchronously executes the canonical Parse, Bind, Validate, Compile,
        /// TypeCheck, Plan, Execute pipeline. Standard execution never constructs ISentence.
        /// </summary>
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
            LegacySentenceAdaptation compatibility = legacySentenceAdapter.Adapt(prompt);
            if (compatibility.IsValid && RequiresCompatibilitySentence(prompt))
            {
                context.Sentence = compatibility.Sentence;
            }
            return await pipeline.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Legacy custom pipeline entry point retained for source compatibility.</summary>
        [Obsolete("Use ExecutionPipeline/ExecuteAsync with canonical compilation stages instead.")]
=======
        /// <summary>Asynchronously runs a prompt with cancellation support.</summary>
        public async Task<ExecutionResult> ExecuteAsync(
            ProcessedPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            ExecutionPipeline pipeline = _pipelineFactory.CreateStandardPipeline();
            var context = new Execution.ExecutionContext(prompt);
            return await pipeline.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute with a custom pipeline configuration.
        /// Allows advanced scenarios with custom execution steps.
        /// </summary>
>>>>>>> origin/agent/stabilize-poc-foundation
        public (ValidationResult ValidationResult, ISentence? Sentence, object? Result) RunWithCustomPipeline(
            ProcessedPrompt prompt,
            Action<ExecutionPipeline> configurePipeline)
        {
<<<<<<< HEAD
            ArgumentNullException.ThrowIfNull(prompt);
            prompt = prompt.WithGrammar(language.Grammar);
=======
>>>>>>> origin/agent/stabilize-poc-foundation
            var pipeline = _pipelineFactory.CreateCustomPipeline(configurePipeline);
            var context = new Execution.ExecutionContext(prompt);
            var result = pipeline.Execute(context);
            ValidationResult compatibilityValidation = result.IsSuccess
                ? result.ValidationResult
<<<<<<< HEAD
                : ValidationResult.Failure(
                    result.Error?.Message ??
                    result.ValidationResult.FailureReason ??
                    "Execution failed.");
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

        private static bool RequiresCompatibilitySentence(ProcessedPrompt prompt) =>
            prompt.Syntax.Commands.FirstOrDefault()?.Verb.Text.ToUpperInvariant() is
                "GET" or "FETCH" or "RETRIEVE" or
                "DOWNLOAD" or "PULL" or "GRAB" or "OBTAIN" or
                "POST";
=======
                : ValidationResult.Failure(result.Error?.Message ?? result.ValidationResult.FailureReason ?? "Execution failed.");
            return (compatibilityValidation, result.Sentence, result.Result);
        }


>>>>>>> origin/agent/stabilize-poc-foundation
    }
}
