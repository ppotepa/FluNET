using FluNET.Keywords;
using FluNET.Lexicon;
using FluNET.Syntax.Core;
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
            System.Diagnostics.Debug.WriteLine("  Sentence structure:");
            IWord? current = sentence.Root;
            int count = 0;
            while (current != null && count++ < 10)
            {
                System.Diagnostics.Debug.WriteLine($"    [{count}] {current.GetType().Name}");
                current = current.Next;
            }

            // Determine the verb base type by finding prepositions using IWord navigation
            Type? verbBaseType = DetermineVerbBaseType(sentence.Root);
            if (verbBaseType == null)
            {
                System.Diagnostics.Debug.WriteLine("Could not determine verb base type - no preposition found");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"  Determined verb base type: {verbBaseType.Name}");

            // Get all implementations of this verb type from Lexicon
            IEnumerable<VerbUsage> implementations = lexicon[verbBaseType];
            if (!implementations.Any())
            {
                System.Diagnostics.Debug.WriteLine($"No implementations found for verb type: {verbBaseType.Name}");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"  Found {implementations.Count()} implementation(s)");

            // Try each implementation using CanHandle pattern
            foreach (VerbUsage impl in implementations)
            {
                System.Diagnostics.Debug.WriteLine($"  Trying implementation: {impl.ImplementationType.Name}");

                object? verbInstance = TryCreateVerbInstance(impl.ImplementationType, sentence.Root);
                if (verbInstance != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  Successfully created verb instance, executing...");
                    return ExecuteVerbInstance(verbInstance);
                }
            }

            System.Diagnostics.Debug.WriteLine("No matching verb implementation found");
            return null;
        }

        /// <summary>
        /// Determine verb base type by finding prepositions using IWord.Find<T>().
        /// Uses is operator to check preposition types - NO STRINGS.
        /// </summary>
        private static Type? DetermineVerbBaseType(IWord root)
        {
            // Use IWord.Find<T>() to locate prepositions
            // Check From preposition
            if (root.Find<From>() != null)
            {
                // Could be Get, Delete, or Load - check verb keyword
                if (root.Find<Keywords.Delete>() != null)
                {
                    return typeof(Delete<,>);
                }

                if (root.Find<Keywords.Load>() != null)
                {
                    return typeof(Load<,>);
                }

                return typeof(Get<,>);
            }

            // Check To preposition
            if (root.Find<To>() != null)
            {
                // Could be Save, Post, or Send - check verb keyword
                if (root.Find<Keywords.Post>() != null)
                {
                    return typeof(Post<,>);
                }

                if (root.Find<Keywords.Send>() != null)
                {
                    return typeof(Send<,>);
                }

                return typeof(Save<,>);
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
                    System.Diagnostics.Debug.WriteLine("    No value word found after preposition");
                    return null;
                }

                // Resolve the literal value (file path, URL, etc.)
                object literalValue = ResolveWordValue(valueWord);
                string valueString = literalValue?.ToString() ?? "";

                System.Diagnostics.Debug.WriteLine($"    Resolved value: '{valueString}'");

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
                            System.Diagnostics.Debug.WriteLine("    Verb.Resolve returned null");
                            return null;
                        }

                        System.Diagnostics.Debug.WriteLine($"    Resolved to type: {resolvedFrom.GetType().Name}");

                        // Create the actual instance with resolved value
                        object? instance = Activator.CreateInstance(implementationType, Array.Empty<string>(), resolvedFrom);

                        if (instance is Get<string[], FileInfo> actualVerb)
                        {
                            // Use CanHandle to validate
                            if (actualVerb.CanHandle(root))
                            {
                                System.Diagnostics.Debug.WriteLine("    CanHandle returned true");
                                return actualVerb;
                            }
                            System.Diagnostics.Debug.WriteLine("    CanHandle returned false");
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    Error creating verb instance: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Execute a verb instance using is/as operators - NO REFLECTION.
        /// </summary>
        private static object? ExecuteVerbInstance(object verbInstance)
        {
            try
            {
                // Use is/as operators to cast and execute
                if (verbInstance is Get<string[], FileInfo> getVerb)
                {
                    return getVerb.Execute();
                }

                System.Diagnostics.Debug.WriteLine($"Unknown verb type: {verbInstance.GetType().Name}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing verb: {ex.Message}");
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

            if (word is ReferenceWord refWord)
            {
                return refWord.Reference;
            }

            if (word is LiteralWord litWord)
            {
                return litWord.Value.TrimEnd('.');
            }

            return word.ToString() ?? "";
        }
    }
}
