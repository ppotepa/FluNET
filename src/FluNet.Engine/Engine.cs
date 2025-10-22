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
        /// Supports THEN clause for chaining multiple commands with shared variable context.
        /// Example: DOWNLOAD [file] FROM http://example.com TO {file.txt} THEN SAY [file].
        /// </summary>
        /// <param name="prompt">The prompt to process</param>
        /// <returns>A tuple containing validation result, the sentence, and execution result</returns>
        public (ValidationResult ValidationResult, ISentence? Sentence, object? Result) Run(ProcessedPrompt prompt)
        {
            // Check if the prompt contains THEN for sentence chaining
            if (ContainsThenClause(prompt))
            {
                return ExecuteChainedSentences(prompt);
            }

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
        /// Check if the prompt contains a THEN keyword for sentence chaining.
        /// </summary>
        private static bool ContainsThenClause(ProcessedPrompt prompt)
        {
            return prompt.Tokens.Any(t => t.Equals("THEN", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Execute chained sentences separated by THEN keyword.
        /// Each sentence shares the same variable context.
        /// Example: DOWNLOAD [file] FROM url TO {file.txt} THEN SAY [file].
        /// </summary>
        private (ValidationResult ValidationResult, ISentence? Sentence, object? Result) ExecuteChainedSentences(ProcessedPrompt prompt)
        {
            // Split the tokens at THEN boundaries
            List<List<string>> sentenceParts = [];
            List<string> currentPart = [];

            foreach (string token in prompt.Tokens)
            {
                if (token.Equals("THEN", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentPart.Count > 0)
                    {
                        sentenceParts.Add(new List<string>(currentPart));
                        currentPart.Clear();
                    }
                }
                else
                {
                    currentPart.Add(token);
                }
            }

            // Add the last part if it exists
            if (currentPart.Count > 0)
            {
                sentenceParts.Add(currentPart);
            }

            // Execute each sentence part sequentially
            object? lastResult = null;
            ISentence? lastSentence = null;

            for (int i = 0; i < sentenceParts.Count; i++)
            {
                // Reconstruct the sentence with terminator
                List<string> part = sentenceParts[i];
                
                // Remove terminator from intermediate sentences, keep only on last
                string lastToken = part[^1];
                bool hasTerminator = lastToken.EndsWith('.') || lastToken.EndsWith('?') || lastToken.EndsWith('!');
                
                if (!hasTerminator && i < sentenceParts.Count - 1)
                {
                    // Add terminator for validation
                    part.Add(".");
                }
                
                string sentenceText = string.Join(" ", part);
                ProcessedPrompt subPrompt = new(sentenceText);

                // Execute this part
                (ValidationResult validation, ISentence? sentence, object? result) = Run(subPrompt);

                if (!validation.IsValid)
                {
                    return (ValidationResult.Failure($"THEN clause {i + 1} failed: {validation.FailureReason}"), sentence, null);
                }

                lastResult = result;
                lastSentence = sentence;
            }

            return (ValidationResult.Success(), lastSentence, lastResult);
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