using FluNET.Compilation;
using FluNET.Execution;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Syntax.Validation;
using FluNET.Variables;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET
{
    /// <summary>Main compilation and execution entry point for FluNET programs.</summary>
    public class Engine
    {
        private readonly ExecutionPipelineFactory _pipelineFactory;
        private readonly IVariableResolver _variableResolver;
        private readonly SemanticCommandBinder _semanticBinder;
        private readonly SemanticProgramValidator _semanticValidator;
        private readonly ExecutionPlanner _executionPlanner;
        private readonly LanguageSnapshot _language;

        /// <summary>Creates the typed FluNET compiler and executor.</summary>
        [ActivatorUtilitiesConstructor]
        public Engine(
            IVariableResolver variableResolver,
            ExecutionPipelineFactory pipelineFactory,
            SemanticCommandBinder semanticBinder,
            ExecutionPlanner executionPlanner,
            LanguageSnapshot language)
        {
            _variableResolver = variableResolver ?? throw new ArgumentNullException(nameof(variableResolver));
            _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
            _semanticBinder = semanticBinder ?? throw new ArgumentNullException(nameof(semanticBinder));
            _executionPlanner = executionPlanner ?? throw new ArgumentNullException(nameof(executionPlanner));
            _language = language ?? throw new ArgumentNullException(nameof(language));
            _semanticValidator = new SemanticProgramValidator(_language);
        }

        /// <summary>
        /// Register a variable that can be used in sentences.
        /// Variables can be referenced using [VariableName] syntax.
        /// </summary>
        public void RegisterVariable<T>(string name, T value)
        {
            _variableResolver.Register(name, value);
        }

        /// <summary>
        /// Parses, binds, validates, and plans a prompt without executing it or
        /// performing external effects through the typed execution pipeline.
        /// </summary>
        public CompilationResult Analyze(ProcessedPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            prompt = prompt.WithGrammar(_language.Grammar);

            FluNetProgram program = new(prompt);
            DiagnosticBag diagnostics = new();
            AddPromptDiagnostics(diagnostics, prompt, prompt.Diagnostics);

            if (!prompt.IsValid)
            {
                string reason = string.Join(" ", prompt.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(reason),
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
                    diagnostics,
                    null,
                    null,
                    CompilationPhase.Parse);
            }

            BoundProgram boundProgram;
            try
            {
                IReadOnlyList<BoundCommand> boundCommands = _semanticBinder.BindProgram(prompt.Syntax);
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
                    diagnostics,
                    null,
                    null,
                    CompilationPhase.Bind);
            }

            DiagnosticBag semanticDiagnostics = _semanticValidator.Validate(boundProgram);
            diagnostics.AddRange(semanticDiagnostics);
            if (semanticDiagnostics.HasErrors)
            {
                string reason = string.Join(" ", semanticDiagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
                return new CompilationResult(
                    program,
                    ValidationResult.Failure(reason),
                    diagnostics,
                    boundProgram,
                    null,
                    CompilationPhase.Validate);
            }

            try
            {
                string? parallelConflict = FindParallelWriteConflict(boundProgram, prompt.Syntax);
                if (parallelConflict is not null)
                    return PlanningFailure(program, boundProgram, diagnostics, prompt, parallelConflict);

                ExecutionPlan plan = _executionPlanner.Create(boundProgram.Commands, prompt.Syntax);
                return new CompilationResult(
                    program,
                    ValidationResult.Success(),
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
        /// Asynchronously executes the Parse, Bind, Validate, Compile, TypeCheck,
        /// Plan, Execute pipeline using typed syntax and an execution plan.
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
            prompt = prompt.WithGrammar(_language.Grammar);
            ExecutionPipeline pipeline = _pipelineFactory.CreateStandardPipeline();
            var context = new Execution.ExecutionContext(prompt, workflowOptions);
            return await pipeline.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
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
