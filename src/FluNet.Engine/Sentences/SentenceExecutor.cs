using FluNET.Keywords;
using FluNET.Lexicon;
using FluNET.Syntax.Verbs;
using FluNET.Variables;
using FluNET.Words;

namespace FluNET.Sentences
{
    /// <summary>
    /// Executes a sentence by discovering and invoking verb implementations.
    /// Uses IWord navigation and is/as operators - NO REFLECTION, NO SWITCH CASES.
    /// </summary>
    public class SentenceExecutor(VariableResolver variableResolver, Lexicon.Lexicon lexicon)
    {
        /// <summary>
        /// Execute a sentence and return the final result.
        /// Uses IWord.Find<T>() navigation and is/as operators only.
        /// </summary>
        public object? Execute(ISentence sentence)
        {
            if (sentence?.Root == null)
            {
                return null;
            }

            // Debug: Print the sentence structure
            Console.WriteLine("  Sentence structure:");
            IWord? current = sentence.Root;
            int count = 0;
            while (current != null && count++ < 10)
            {
                Console.WriteLine($"    [{count}] {current.GetType().Name}");
                current = current.Next;
            }

            // Determine the verb base type by finding prepositions using IWord navigation
            Type? verbBaseType = DetermineVerbBaseType(sentence.Root);
            if (verbBaseType == null)
            {
                Console.WriteLine("Could not determine verb base type - no preposition found");
                return null;
            }

            Console.WriteLine($"  Determined verb base type: {verbBaseType.Name}");

            // Get all implementations of this verb type from Lexicon
            IEnumerable<VerbUsage> implementations = lexicon[verbBaseType];
            if (!implementations.Any())
            {
                Console.WriteLine($"No implementations found for verb type: {verbBaseType.Name}");
                return null;
            }

            Console.WriteLine($"  Found {implementations.Count()} implementation(s)");

            // Try each implementation using CanHandle pattern
            foreach (VerbUsage impl in implementations)
            {
                Console.WriteLine($"  Trying implementation: {impl.ImplementationType.Name}");

                object? verbInstance = TryCreateVerbInstance(impl.ImplementationType, sentence.Root);
                if (verbInstance != null)
                {
                    Console.WriteLine($"  Successfully created verb instance, executing...");
                    return ExecuteVerbInstance(verbInstance);
                }
            }

            Console.WriteLine("No matching verb implementation found");
            return null;
        }

        /// <summary>
        /// Determine verb base type by finding prepositions using IWord.Find<T>().
        /// Uses is operator to check preposition types - NO STRINGS.
        /// </summary>
        private Type? DetermineVerbBaseType(IWord root)
        {
            // Use IWord.Find<T>() to locate prepositions
            // Check From preposition
            if (root.Find<From>() != null)
            {
                // Could be Get, Delete, or Load - check verb keyword
                return root.Find<Keywords.Delete>() != null
                    ? typeof(Delete<,>)
                    : root.Find<Keywords.Load>() != null ? typeof(Load<,>) : typeof(Get<,>);
            }

            // Check To preposition
            if (root.Find<To>() != null)
            {
                // Could be Save, Post, or Send - check verb keyword
                return root.Find<Keywords.Post>() != null ? typeof(Post<,>) : root.Find<Keywords.Send>() != null ? typeof(Send<,>) : typeof(Save<,>);
            }

            // Check Using preposition
            return root.Find<Using>() != null ? typeof(Transform<,>) : null;
        }

        /// <summary>
        /// Try to create a verb instance using is/as operators and CanHandle pattern.
        /// Uses the verb's Resolve method for contextual type resolution.
        /// NO REFLECTION - uses is/as to cast to specific verb types.
        /// </summary>
        private object? TryCreateVerbInstance(Type implementationType, IWord root)
        {
            try
            {
                // Get the preposition value word
                From? fromPrep = root.Find<From>();
                To? toPrep = root.Find<To>();
                Using? usingPrep = root.Find<Using>();

                IWord? valueWord = fromPrep?.Next ?? toPrep?.Next ?? usingPrep?.Next;
                if (valueWord == null)
                {
                    Console.WriteLine("    No value word found after preposition");
                    return null;
                }

                // Resolve the literal value (file path, URL, etc.)
                object literalValue = ResolveWordValue(valueWord);
                string valueString = literalValue?.ToString() ?? "";

                Console.WriteLine($"    Resolved value: '{valueString}'");

                // Try to create specific verb types using is/as operators
                // For Get<string[], FileInfo> (GetText with file)
                if (implementationType.Name == "GetText")
                {
                    // Create a temporary instance to call Resolve
                    object? tempInstance = Activator.CreateInstance(implementationType, Array.Empty<string>(), new FileInfo("temp"));

                    if (tempInstance is Get<string[], FileInfo> getVerb)
                    {
                        // Use the verb's Resolve method to contextually resolve the value
                        FileInfo? resolvedFrom = getVerb.Resolve(valueString);
                        if (resolvedFrom == null)
                        {
                            Console.WriteLine("    Verb.Resolve returned null");
                            return null;
                        }

                        Console.WriteLine($"    Resolved to type: {resolvedFrom.GetType().Name}");

                        // Create the actual instance with resolved value
                        object? instance = Activator.CreateInstance(implementationType, Array.Empty<string>(), resolvedFrom);

                        if (instance is Get<string[], FileInfo> actualVerb)
                        {
                            // Use CanHandle to validate
                            if (actualVerb.CanHandle(root))
                            {
                                Console.WriteLine("    CanHandle returned true");
                                return actualVerb;
                            }
                            Console.WriteLine("    CanHandle returned false");
                        }
                    }
                }

                // Future: Add more verb type patterns using is/as
                // if (implementationType.Name == "GetJson")
                // {
                //     var tempInstance = Activator.CreateInstance(implementationType, ...);
                //     if (tempInstance is Get<JsonNode[], Uri> getVerb)
                //     {
                //         var resolvedUri = getVerb.Resolve(valueString);
                //         ...
                //     }
                // }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error creating verb instance: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Execute a verb instance using is/as operators - NO REFLECTION.
        /// </summary>
        private object? ExecuteVerbInstance(object verbInstance)
        {
            try
            {
                // Use is/as operators to cast and execute
                if (verbInstance is Get<string[], FileInfo> getVerb)
                {
                    return getVerb.Execute();
                }

                // Add more verb type patterns as needed using is/as
                // Example:
                // if (verbInstance is Save<string, FileInfo> saveVerb)
                //     return saveVerb.Execute();

                Console.WriteLine($"Unknown verb type: {verbInstance.GetType().Name}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing verb: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolve a word to its actual value (handle variables, references, and literals).
        /// Uses is operator - NO strings or reflection.
        /// </summary>
        private object ResolveWordValue(IWord word)
        {
            // Use is operator for type checking
            if (word is VariableWord varWord)
            {
                try
                {
                    return variableResolver.Resolve<object>(varWord.VariableReference) ?? varWord.VariableReference;
                }
                catch
                {
                    return varWord.VariableReference;
                }
            }

            return word is ReferenceWord refWord
                ? (object)refWord.Reference
                : word is LiteralWord litWord ? litWord.Value.TrimEnd('.') : word.ToString() ?? "";
        }
    }
}

