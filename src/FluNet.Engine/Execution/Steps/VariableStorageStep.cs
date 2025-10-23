using FluNET.Matching;
using FluNET.Syntax.Core;
using FluNET.Variables;
using FluNET.Words;
using System.Text.Json;

namespace FluNET.Execution.Steps
{
    /// <summary>
    /// Step 5: Store execution results in variables if needed
    /// Handles both simple variables [name] and destructuring [{prop1, prop2}]
    /// </summary>
    public class VariableStorageStep : IExecutionStep
    {
        private readonly IVariableResolver _variableResolver;
        private readonly MatcherResolver _matcherResolver;

        public VariableStorageStep(IVariableResolver variableResolver, MatcherResolver matcherResolver)
        {
            _variableResolver = variableResolver ?? throw new ArgumentNullException(nameof(variableResolver));
            _matcherResolver = matcherResolver ?? throw new ArgumentNullException(nameof(matcherResolver));
        }

        public ExecutionResult Execute(ExecutionContext context, Func<ExecutionContext, ExecutionResult> next)
        {
            if (context.Sentence == null || context.Result == null)
            {
                // No sentence or result, continue to next step
                return next(context);
            }

            try
            {
                // Store result in variable if the verb's direct object is a VariableWord
                StoreResultInVariableIfNeeded(context.Sentence.Root, context.Result);

                // Continue to next step
                return next(context);
            }
            catch (Exception ex)
            {
                // Variable storage errors shouldn't stop execution
                System.Diagnostics.Debug.WriteLine($"Variable storage warning: {ex.Message}");
                return next(context);
            }
        }

        /// <summary>
        /// If the verb's direct object (first word after verb) is a VariableWord,
        /// store the execution result in that variable.
        /// Supports destructuring syntax: [{prop1, prop2, prop3}]
        /// </summary>
        private void StoreResultInVariableIfNeeded(IWord? root, object result)
        {
            if (root == null) return;

            // Check if the first word after the verb is a VariableWord
            IWord? firstWord = root.Next;
            if (firstWord is not VariableWord varWord)
            {
                return;
            }

            string varRef = varWord.VariableReference.TrimEnd('.');
            string innerContent = varRef.TrimStart('[').TrimEnd(']');

            var destructuringMatcher = _matcherResolver.GetMatcher<IDestructuringMatcher>();

            // Check for destructuring syntax: [{prop1, prop2, prop3}]
            if (destructuringMatcher.IsMatch(innerContent))
            {
                HandleDestructuring(result, destructuringMatcher, innerContent);
            }
            else
            {
                // Simple variable: [text]
                _variableResolver.Register(innerContent, result);
                System.Diagnostics.Debug.WriteLine($"Stored result in variable [{innerContent}]");
            }
        }

        /// <summary>
        /// Handle destructuring pattern: extract properties from result and store as individual variables
        /// </summary>
        private void HandleDestructuring(object result, IDestructuringMatcher matcher, string innerContent)
        {
            string[] propertyNames = matcher.GetPropertyNames(innerContent);

            if (TryExtractPropertiesFromResult(result, propertyNames, out Dictionary<string, object>? extractedProps))
            {
                foreach (var kvp in extractedProps!)
                {
                    _variableResolver.Register(kvp.Key, kvp.Value);
                    System.Diagnostics.Debug.WriteLine($"Stored property in variable [{kvp.Key}] = {kvp.Value}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract properties {string.Join(", ", propertyNames)} from result");
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
                    jsonDoc = JsonDocument.Parse(jsonString);
                }
                else if (result is string[] lines)
                {
                    string combined = string.Join('\n', lines);
                    if (combined.TrimStart().StartsWith('{') || combined.TrimStart().StartsWith('['))
                    {
                        jsonDoc = JsonDocument.Parse(combined);
                    }
                }
                else if (result is Dictionary<string, object> dict)
                {
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
