using FluNET.Execution;
using FluNET.Matching;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using System.Text.Json;

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
        /// Parses and validates a prompt without executing it or performing external effects.
        /// </summary>
        public PromptAnalysis Analyze(ProcessedPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);

            if (!prompt.IsValid)
            {
                string reason = string.Join(" ", prompt.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
                return new PromptAnalysis(prompt, ValidationResult.Failure(reason), null);
            }

            try
            {
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
        public (ValidationResult ValidationResult, ISentence? Sentence, object? Result) RunWithCustomPipeline(
            ProcessedPrompt prompt,
            Action<ExecutionPipeline> configurePipeline)
        {
            var pipeline = _pipelineFactory.CreateCustomPipeline(configurePipeline);
            var context = new Execution.ExecutionContext(prompt);
            var result = pipeline.Execute(context);
            ValidationResult compatibilityValidation = result.IsSuccess
                ? result.ValidationResult
                : ValidationResult.Failure(result.Error?.Message ?? result.ValidationResult.FailureReason ?? "Execution failed.");
            return (compatibilityValidation, result.Sentence, result.Result);
        }


    }
}
