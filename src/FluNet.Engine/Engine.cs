using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Variables;

namespace FluNET
{
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly SentenceFactory sentenceFactory;
        private readonly SentenceValidator sentenceValidator;
        private readonly VariableResolver variableResolver;
        private readonly SentenceExecutor sentenceExecutor;

        public Engine(TokenTreeFactory tokenTreeFactory, SentenceFactory sentenceFactory,
            SentenceValidator sentenceValidator, VariableResolver variableResolver,
            SentenceExecutor sentenceExecutor)
        {
            this.tokenTreeFactory = tokenTreeFactory;
            this.sentenceFactory = sentenceFactory;
            this.sentenceValidator = sentenceValidator;
            this.variableResolver = variableResolver;
            this.sentenceExecutor = sentenceExecutor;
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
        /// Parse, validate, and execute a sentence.
        /// </summary>
        /// <param name="prompt">The prompt to process</param>
        /// <returns>A tuple containing validation result, the sentence, and execution result</returns>
        public (ValidationResult ValidationResult, ISentence? Sentence, object? Result) Run(ProcessedPrompt prompt)
        {
            TokenTree tree = tokenTreeFactory.Process(prompt);

            // Validate the sentence structure
            ValidationResult validationResult = sentenceValidator.ValidateSentence(tree);
            if (!validationResult.IsValid)
            {
                return (validationResult, null, null);
            }

            // Create the sentence from the validated tree
            ISentence? sentence = sentenceFactory.CreateFromTree(tree);

            // Sentence should not be null at this point since validation passed
            if (sentence == null)
            {
                return (ValidationResult.Failure("Failed to create sentence from validated tree"), null, null);
            }

            // Execute the sentence
            object? result = null;
            try
            {
                result = sentenceExecutor.Execute(sentence);
            }
            catch (Exception ex)
            {
                return (ValidationResult.Failure($"Execution failed: {ex.Message}"), sentence, null);
            }

            return (validationResult, sentence, result);
        }
    }
}