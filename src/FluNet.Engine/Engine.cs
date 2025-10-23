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
    public class Engine
    {
        private readonly TokenTreeFactory tokenTreeFactory;
        private readonly SentenceFactory sentenceFactory;
        private readonly SentenceValidator sentenceValidator;
        private readonly IVariableResolver variableResolver;
        private readonly SentenceExecutor sentenceExecutor;
        private readonly MatcherResolver matcherResolver;

        public Engine(TokenTreeFactory tokenTreeFactory, SentenceFactory sentenceFactory,
            SentenceValidator sentenceValidator, IVariableResolver variableResolver,
            SentenceExecutor sentenceExecutor, MatcherResolver matcherResolver)
        {
            this.tokenTreeFactory = tokenTreeFactory;
            this.sentenceFactory = sentenceFactory;
            this.sentenceValidator = sentenceValidator;
            this.variableResolver = variableResolver;
            this.sentenceExecutor = sentenceExecutor;
            this.matcherResolver = matcherResolver;
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
            TokenTree tree = tokenTreeFactory.Process(prompt);

            // Validate the sentence structure
            ValidationResult validationResult = sentenceValidator.ValidateSentence(tree);
            if (!validationResult.IsValid)
            {
                return (validationResult, null, null);
            }

            // Create the sentence from the validated tree (may contain sub-sentences)
            ISentence? sentence = sentenceFactory.CreateFromTree(tree);

            // Sentence should not be null at this point since validation passed
            if (sentence == null)
            {
                return (ValidationResult.Failure("Failed to create sentence from validated tree"), null, null);
            }

            // Execute the sentence and any sub-sentences
            object? result = null;
            try
            {
                result = ExecuteSentenceWithSubSentences(sentence);
            }
            catch (Exception ex)
            {
                return (ValidationResult.Failure($"Execution failed: {ex.Message}"), sentence, null);
            }

            return (validationResult, sentence, result);
        }

        /// <summary>
        /// Execute a sentence and its sub-sentences sequentially.
        /// All sub-sentences share the same variable context.
        /// </summary>
        private object? ExecuteSentenceWithSubSentences(ISentence sentence)
        {
            // Execute main sentence
            object? result = sentenceExecutor.Execute(sentence);

            // Auto-store result in variable if needed
            if (result != null && sentence.Root != null)
            {
                StoreResultInVariableIfNeeded(sentence.Root, result);
            }

            // Execute sub-sentences (THEN clauses)
            foreach (ISentence subSentence in sentence.SubSentences)
            {
                result = sentenceExecutor.Execute(subSentence);

                // Auto-store result in variable if needed
                if (result != null && subSentence.Root != null)
                {
                    StoreResultInVariableIfNeeded(subSentence.Root, result);
                }
            }

            // Return the result of the last executed sentence
            return result;
        }        /// <summary>
                 /// If the verb's direct object (first word after verb) is a VariableWord,
                 /// store the execution result in that variable.
                 /// Supports destructuring syntax: [{prop1, prop2, prop3}]
                 /// Example: GET [text] FROM file.txt -> [text] = file contents
                 /// Example: GET [{name, age}] FROM {user} -> [name] = "John", [age] = 30
                 /// </summary>
        private void StoreResultInVariableIfNeeded(IWord root, object result)
        {
            // Check if the first word after the verb is a VariableWord
            IWord? firstWord = root.Next;
            if (firstWord is VariableWord varWord)
            {
                string varRef = varWord.VariableReference.TrimEnd('.');

                // Remove brackets for destructuring check
                string innerContent = varRef.TrimStart('[').TrimEnd(']');

                // Get matchers
                var destructuringMatcher = matcherResolver.GetMatcher<IDestructuringMatcher>();

                // Check for destructuring syntax: [{prop1, prop2, prop3}]
                if (destructuringMatcher.IsMatch(innerContent))
                {
                    // Extract property names using the matcher
                    string[] propertyNames = destructuringMatcher.GetPropertyNames(innerContent);

                    // Try to parse result as JSON and extract properties
                    if (TryExtractPropertiesFromResult(result, propertyNames, out Dictionary<string, object>? extractedProps))
                    {
                        // Store each property as individual variable
                        foreach (var kvp in extractedProps!)
                        {
                            variableResolver.Register(kvp.Key, kvp.Value);
                            System.Diagnostics.Debug.WriteLine($"Stored property in variable [{kvp.Key}] = {kvp.Value}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to extract properties {string.Join(", ", propertyNames)} from result");
                    }
                }
                else
                {
                    // Simple variable: [text]
                    string varName = innerContent;

                    // Store the result in the variable
                    variableResolver.Register(varName, result);
                    System.Diagnostics.Debug.WriteLine($"Stored result in variable [{varName}]");
                }
            }
        }

        /// <summary>
        /// Try to extract specified properties from a result object.
        /// Supports JSON strings, string arrays (auto-detects JSON), and dictionaries.
        /// </summary>
        private bool TryExtractPropertiesFromResult(object result, string[] propertyNames, out Dictionary<string, object>? properties)
        {
            properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            try
            {
                JsonDocument? jsonDoc = null;

                // Handle different result types
                if (result is string jsonString)
                {
                    // Direct JSON string
                    jsonDoc = JsonDocument.Parse(jsonString);
                }
                else if (result is string[] lines)
                {
                    // Array of strings - join and try to parse as JSON
                    string combined = string.Join('\n', lines);
                    if (combined.TrimStart().StartsWith('{') || combined.TrimStart().StartsWith('['))
                    {
                        jsonDoc = JsonDocument.Parse(combined);
                    }
                }
                else if (result is Dictionary<string, object> dict)
                {
                    // Already a dictionary - extract directly
                    foreach (string propName in propertyNames)
                    {
                        if (dict.TryGetValue(propName, out object? value))
                        {
                            properties[propName] = value;
                        }
                    }
                    return properties.Count > 0;
                }

                // Extract properties from JSON document
                if (jsonDoc != null)
                {
                    JsonElement root = jsonDoc.RootElement;
                    foreach (string propName in propertyNames)
                    {
                        if (root.TryGetProperty(propName, out JsonElement element))
                        {
                            properties[propName] = ExtractJsonValue(element);
                        }
                    }
                    jsonDoc.Dispose();
                    return properties.Count > 0;
                }
            }
            catch (JsonException)
            {
                // Not valid JSON
            }

            properties = null;
            return false;
        }

        /// <summary>
        /// Extract a value from a JsonElement as the appropriate .NET type
        /// </summary>
        private object ExtractJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt32(out int intVal) ? intVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Array => element.EnumerateArray().Select(ExtractJsonValue).ToArray(),
                JsonValueKind.Object => element.Deserialize<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                _ => element.ToString()
            };
        }
    }
}