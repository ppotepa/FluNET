using FluNET.Keywords;
using FluNET.Lexicon;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Registry;
using FluNET.Syntax.Verbs;
using FluNET.Variables;
using FluNET.Words;
using System.Reflection;

namespace FluNET.Sentences
{
    /// <summary>
    /// Executes a sentence by discovering and invoking verb implementations.
    /// Uses minimal reflection - only for type activation.
    /// </summary>
    public class SentenceExecutor
    {
        private readonly IVariableResolver _variableResolver;
        private readonly Lexicon.Lexicon _lexicon;
        private readonly VerbRegistry _verbRegistry;
        private readonly DiscoveryService _discoveryService;

        public SentenceExecutor(IVariableResolver variableResolver, Lexicon.Lexicon lexicon, VerbRegistry verbRegistry, DiscoveryService discoveryService)
        {
            _variableResolver = variableResolver;
            _lexicon = lexicon;
            _verbRegistry = verbRegistry;
            _discoveryService = discoveryService;
        }

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

            // Determine the verb base type by checking interfaces on the verb
            Type? verbBaseType = DetermineVerbBaseType(sentence.Root);
            if (verbBaseType == null)
            {
                System.Diagnostics.Debug.WriteLine("Could not determine verb base type - no preposition found");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"  Determined verb base type: {verbBaseType.Name}");

            // Get all implementations of this verb type from Lexicon
            IEnumerable<VerbUsage> implementations = _lexicon[verbBaseType];
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
        /// Determine verb base type using DiscoveryService dictionary lookup.
        /// This supports all verb names and synonyms automatically.
        /// </summary>
        private Type? DetermineVerbBaseType(IWord root)
        {
            return _discoveryService.GetVerbBaseTypeByWord(root);
        }

        /// <summary>
        /// Try to create a verb instance using interface detection and CanHandle pattern.
        /// Uses VerbRegistry to avoid reflection.
        /// </summary>
        private object? TryCreateVerbInstance(Type implementationType, IWord root)
        {
            try
            {
                // Get parameter info from VerbRegistry (cached, no reflection needed)
                var (hasWhat, hasFrom, hasTo, hasUsing) = _verbRegistry.GetVerbParameterInfo(implementationType);

                System.Diagnostics.Debug.WriteLine($"Creating {implementationType.Name}: hasWhat={hasWhat}, hasFrom={hasFrom}, hasTo={hasTo}, hasUsing={hasUsing}");

                // Create a temporary instance using parameterless constructor to access Resolve methods
                object? tempInstance = Activator.CreateInstance(implementationType);
                if (tempInstance == null)
                {
                    System.Diagnostics.Debug.WriteLine("    Failed to create temp instance with parameterless constructor");
                    return null;
                }

                // Resolve parameters based on verb requirements
                List<object?> constructorParams = new();

                // Resolve WHAT parameter if needed
                if (hasWhat)
                {
                    object? whatParam = ResolveWhatParameter(root, implementationType, tempInstance);
                    if (whatParam == null && !hasFrom && !hasTo && !hasUsing)
                    {
                        // For WHAT-only verbs (like SAY), null is not allowed
                        System.Diagnostics.Debug.WriteLine("    WHAT parameter resolved to null");
                        return null;
                    }
                    constructorParams.Add(whatParam);
                }

                // Resolve FROM parameter if needed
                if (hasFrom)
                {
                    From? fromKeyword = root.Find<From>();
                    object? fromParam = ResolvePrepositionParameter(root, implementationType, tempInstance, fromKeyword, "Resolve");

                    // FROM is optional if the keyword isn't in the sentence
                    // FROM is required if the keyword is present but has no value
                    if (fromParam == null && fromKeyword != null && fromKeyword.Next != null)
                    {
                        System.Diagnostics.Debug.WriteLine("    FROM keyword present but parameter resolved to null");
                        return null;
                    }
                    constructorParams.Add(fromParam);
                }

                // Resolve TO parameter if needed
                if (hasTo)
                {
                    To? toKeyword = root.Find<To>();
                    // Try "ResolveTo" first (for verbs like DOWNLOAD that have both FROM and TO)
                    // Fall back to "Resolve" if ResolveTo doesn't exist (for verbs with only TO)
                    string resolveMethodName = implementationType.GetMethod("ResolveTo") != null ? "ResolveTo" : "Resolve";
                    object? toParam = ResolvePrepositionParameter(root, implementationType, tempInstance, toKeyword, resolveMethodName);

                    // TO is optional if the keyword isn't in the sentence
                    // TO is required if the keyword is present but has no value
                    if (toParam == null && toKeyword != null && toKeyword.Next != null)
                    {
                        System.Diagnostics.Debug.WriteLine("    TO keyword present but parameter resolved to null");
                        return null;
                    }
                    constructorParams.Add(toParam);
                }

                // Resolve USING parameter if needed
                if (hasUsing)
                {
                    Using? usingKeyword = root.Find<Using>();
                    object? usingParam = ResolvePrepositionParameter(root, implementationType, tempInstance, usingKeyword, "Resolve");

                    // USING is optional if the keyword isn't in the sentence
                    // USING is required if the keyword is present but has no value
                    if (usingParam == null && usingKeyword != null && usingKeyword.Next != null)
                    {
                        System.Diagnostics.Debug.WriteLine("    USING keyword present but parameter resolved to null");
                        return null;
                    }
                    constructorParams.Add(usingParam);
                }

                // Use VerbRegistry to create the instance (no constructor reflection needed!)
                object? instance = _verbRegistry.CreateVerbInstance(implementationType, constructorParams.ToArray());

                if (instance == null)
                {
                    System.Diagnostics.Debug.WriteLine("    Failed to create instance with resolved parameters");
                    return null;
                }

                // Validate using CanHandle method if it exists
                MethodInfo? canHandleMethod = implementationType.GetMethod("CanHandle");
                if (canHandleMethod != null)
                {
                    object? canHandleResult = canHandleMethod.Invoke(instance, new object[] { root });
                    if (canHandleResult is bool canHandle && !canHandle)
                    {
                        System.Diagnostics.Debug.WriteLine("    CanHandle returned false");
                        return null;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"    {implementationType.Name} instance created and validated successfully");
                return instance;
            }
            catch (InvalidOperationException)
            {
                // Re-throw InvalidOperationException (e.g., undefined variables) - these are real errors
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"    Exception creating verb: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolve the WHAT parameter for a verb.
        /// </summary>
        private object? ResolveWhatParameter(IWord root, Type implementationType, object tempInstance)
        {
            // Determine if this is a retrieval verb (GET/LOAD/DOWNLOAD) where WHAT is OUTPUT
            // versus an action verb where WHAT is INPUT
            Type? baseType = implementationType.BaseType;
            bool isRetrievalVerb = baseType != null &&
                (baseType.Name.StartsWith("Get") || baseType.Name.StartsWith("Load") || baseType.Name.StartsWith("Download"));

            System.Diagnostics.Debug.WriteLine($"    Verb base type: {baseType?.Name}, isRetrievalVerb: {isRetrievalVerb}");

            // Collect words until we hit a preposition
            string whatString = CollectWordsUntilPreposition(root.Next, isRetrievalVerb);
            System.Diagnostics.Debug.WriteLine($"    Resolved WHAT: '{whatString}' (isRetrievalVerb={isRetrievalVerb})");

            // Try to call ResolveWhat method if it exists on the verb
            return TryInvokeResolveMethod(implementationType, tempInstance, "ResolveWhat", typeof(string), whatString) ?? whatString;
        }

        /// <summary>
        /// Collects words from the sentence until a preposition is encountered.
        /// For retrieval verbs, keeps variable names unresolve (output targets).
        /// For action verbs, resolves variables to their values (inputs).
        /// </summary>
        private string CollectWordsUntilPreposition(IWord? startWord, bool isRetrievalVerb)
        {
            List<string> parts = new();
            IWord? currentWord = startWord;

            while (currentWord != null && !(currentWord is From || currentWord is To || currentWord is Using))
            {
                // For retrieval verbs, if WHAT is a variable, don't resolve it - it's an output target
                if (isRetrievalVerb && currentWord is Words.VariableWord varWord)
                {
                    string varName = varWord.VariableReference.TrimStart('[').TrimEnd(']').TrimEnd('.');
                    parts.Add(varName);
                    System.Diagnostics.Debug.WriteLine($"    Output variable detected: [{varName}] (not resolving - will store result here)");
                }
                else
                {
                    // For action verbs or literals, resolve normally
                    object? resolvedValue = ResolveWordValue(currentWord);

                    // Handle arrays specially
                    if (resolvedValue is string[] stringArray)
                    {
                        foreach (string line in stringArray)
                        {
                            if (!string.IsNullOrEmpty(line))
                            {
                                parts.Add(line.TrimEnd('.'));
                            }
                        }
                    }
                    else
                    {
                        string? resolved = resolvedValue?.ToString();
                        if (!string.IsNullOrEmpty(resolved))
                        {
                            parts.Add(resolved.TrimEnd('.'));
                        }
                    }
                }
                currentWord = currentWord.Next;
            }

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Resolve a preposition parameter (FROM/TO/USING) for a verb.
        /// </summary>
        private object? ResolvePrepositionParameter(IWord root, Type implementationType, object tempInstance, IWord? preposition, string resolveMethodName)
        {
            if (preposition?.Next == null)
            {
                return null;
            }

            IWord valueWord = preposition.Next;

            // Try typed overloads first (Resolve(ReferenceWord), Resolve(VariableWord))
            if (valueWord is ReferenceWord refWord)
            {
                object? result = TryInvokeResolveMethod(implementationType, tempInstance, resolveMethodName, typeof(ReferenceWord), refWord);
                if (result != null)
                {
                    System.Diagnostics.Debug.WriteLine($"    {resolveMethodName}(ReferenceWord) result: {result.GetType().Name}");
                    return result;
                }
            }

            if (valueWord is VariableWord varWord)
            {
                object? result = TryInvokeResolveMethod(implementationType, tempInstance, resolveMethodName, typeof(VariableWord), varWord);
                if (result != null)
                {
                    System.Diagnostics.Debug.WriteLine($"    {resolveMethodName}(VariableWord) result: {result.GetType().Name}");
                    return result;
                }
            }

            // Fallback: resolve to string and call Resolve(string)
            object? valueObj = ResolveWordValue(valueWord);
            string valueString = valueObj?.ToString() ?? "";
            System.Diagnostics.Debug.WriteLine($"    Resolved {resolveMethodName} input: '{valueString}'");

            object? stringResult = TryInvokeResolveMethod(implementationType, tempInstance, resolveMethodName, typeof(string), valueString);
            if (stringResult != null)
            {
                System.Diagnostics.Debug.WriteLine($"    {resolveMethodName} result: {stringResult.GetType().Name}");
                return stringResult;
            }

            // Fallback: return the string value
            return valueString;
        }

        /// <summary>
        /// Attempts to invoke a resolve method with the given parameter type.
        /// Returns null if the method doesn't exist.
        /// </summary>
        private static object? TryInvokeResolveMethod(Type implementationType, object tempInstance, string methodName, Type parameterType, object parameter)
        {
            MethodInfo? method = implementationType.GetMethod(methodName, new[] { parameterType });
            return method?.Invoke(tempInstance, new object[] { parameter });
        }

        /// <summary>
        /// Execute a verb instance by calling its Invoke() method.
        /// All verbs implement IVerb<TWhat, TFrom> which has the Invoke() method.
        /// </summary>
        private static object? ExecuteVerbInstance(object verbInstance)
        {
            try
            {
                Type verbType = verbInstance.GetType();
                System.Diagnostics.Debug.WriteLine($"Executing verb: {verbType.Name}");

                // Find and invoke the Invoke() method
                MethodInfo? invokeMethod = verbType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
                if (invokeMethod == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Verb {verbType.Name} does not have an Invoke() method");
                    return null;
                }

                // Call Invoke() - it takes no parameters and returns TWhat
                object? result = invokeMethod.Invoke(verbInstance, null);
                System.Diagnostics.Debug.WriteLine($"Verb execution result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing verb: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
                // Variables MUST be resolved from context
                // Remove trailing period but keep brackets for resolver: [greeting]. -> [greeting]
                string varReference = varWord.VariableReference.TrimEnd('.');
                var resolved = _variableResolver.Resolve<object>(varReference);

                if (resolved == null)
                {
                    throw new InvalidOperationException(
                        $"Variable {varReference} not found in context. " +
                        $"Variables must be stored before use with commands like: GET {varReference} FROM file.txt");
                }

                return resolved;
            }

            if (word is ReferenceWord refWord)
            {
                // References are inline values - strip braces and return literal value
                // The verb's Resolve() method will convert to appropriate type
                return refWord.Reference.TrimStart('{').TrimEnd('.').TrimEnd('}');
            }

            if (word is LiteralWord litWord)
            {
                // Strip quotes and trailing period
                string value = litWord.Value.TrimEnd('.');

                // Remove surrounding quotes if present (for quoted strings like "hello" or "")
                if (value.StartsWith('"') && value.EndsWith('"'))
                {
                    value = value.Substring(1, Math.Max(0, value.Length - 2));
                }

                return value;
            }

            //  If the word is a verb/keyword (e.g., ECHO appearing in message text),
            // return its text representation
            // EXCEPTION: QualifierWord when used as data (not as a type qualifier)
            // In contexts like "TRANSFORM ASCII text USING ...", "text" should stay lowercase
            // even though it matches the TEXT qualifier
            if (word is IKeyword keyword)
            {
                // QualifierWords are uppercase by default, but when used as actual data
                // (not as type qualifiers), we need to preserve case.
                // However, we don't have the original token here.
                // For now, lowercase qualifier keywords when they appear as data.
                if (word is Words.QualifierWord qualWord)
                {
                    // If this appears to be used as data (not a qualifier), lowercase it
                    // This is a heuristic: qualifiers after verbs like TRANSFORM/SAY are data
                    return qualWord.Text.ToLowerInvariant();
                }

                return keyword.Text;
            }

            return word.ToString() ?? "";
        }
    }
}