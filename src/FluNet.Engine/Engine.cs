using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;

namespace FluNET
{
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly SentenceFactory sentenceFactory;
        private readonly SentenceValidator sentenceValidator;
        private readonly IVariableResolver variableResolver;
        private readonly SentenceExecutor sentenceExecutor;

        public Engine(TokenTreeFactory tokenTreeFactory, SentenceFactory sentenceFactory,
            SentenceValidator sentenceValidator, IVariableResolver variableResolver,
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

                // Auto-store result in variable if the verb's direct object is a VariableWord
                // Example: GET [text] FROM file.txt -> stores result in [text]
                if (result != null && sentence.Root != null)
                {
                    StoreResultInVariableIfNeeded(sentence.Root, result);
                }
            }
            catch (Exception ex)
            {
                return (ValidationResult.Failure($"Execution failed: {ex.Message}"), sentence, null);
            }

            return (validationResult, sentence, result);
        }

        /// <summary>
        /// If the verb's direct object (first word after verb) is a VariableWord,
        /// store the execution result in that variable.
        /// Example: GET [text] FROM file.txt -> [text] = file contents
        /// </summary>
        private void StoreResultInVariableIfNeeded(IWord root, object result)
        {
            // Check if the first word after the verb is a VariableWord
            IWord? firstWord = root.Next;
            if (firstWord is VariableWord varWord)
            {
                // Extract variable name without brackets: [text] -> text
                string varName = varWord.VariableReference
                    .TrimStart('[')
                    .TrimEnd(']')
                    .TrimEnd('.');

                // Store the result in the variable
                variableResolver.Register(varName, result);
                System.Diagnostics.Debug.WriteLine($"Stored result in variable [{varName}]");
            }
        }
    }
}