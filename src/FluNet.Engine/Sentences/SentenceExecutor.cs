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

        public SentenceExecutor(IVariableResolver variableResolver, Lexicon.Lexicon lexicon, VerbRegistry verbRegistry)
        {
            _variableResolver = variableResolver;
            _lexicon = lexicon;
            _verbRegistry = verbRegistry;
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
        /// Determine verb base type by checking what interfaces the verb implements.
        /// This is determined from the verb instance's interface implementation, not text matching.
        /// </summary>
        private static Type? DetermineVerbBaseType(IWord root)
        {
            if (root is not IVerb verb)
            {
                return null;
            }

            Type verbType = verb.GetType();
            Type[] interfaces = verbType.GetInterfaces();

            // Check if verb implements IFrom and ITo (both) - indicates Download pattern
            bool hasFrom = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IFrom<>));
            bool hasTo = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITo<>));
            bool hasWhat = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWhat<>));
            bool hasUsing = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUsing<>));

            if (hasFrom && hasTo)
            {
                // Has both FROM and TO - Download pattern
                return typeof(Download<,,>);
            }

            if (hasFrom && !hasTo && !hasUsing)
            {
                // Has FROM only - could be Get, Delete, or Load
                // Use the verb's Text property to determine which one
                if (verb is IKeyword keyword)
                {
                    string verbText = keyword.Text.ToUpperInvariant();
                    return verbText switch
                    {
                        "GET" => typeof(Get<,>),
                        "DELETE" => typeof(Delete<,>),
                        "LOAD" => typeof(Load<,>),
                        _ => typeof(Get<,>) // Default to Get for unknown verbs with FROM
                    };
                }
                return typeof(Get<,>);
            }

            if (hasTo && !hasFrom && !hasUsing)
            {
                // Has TO only - could be Save, Post, or Send
                // Use the verb's Text property to determine which one
                if (verb is IKeyword keyword)
                {
                    string verbText = keyword.Text.ToUpperInvariant();
                    return verbText switch
                    {
                        "SAVE" => typeof(Save<,>),
                        "POST" => typeof(Post<,>),
                        "SEND" => typeof(Send<,>),
                        _ => typeof(Save<,>) // Default to Save for unknown verbs with TO
                    };
                }
                return typeof(Save<,>);
            }

            if (hasUsing)
            {
                // Has USING - Transform pattern
                return typeof(Transform<,>);
            }

            if (hasWhat && !hasFrom && !hasTo && !hasUsing)
            {
                // Has only WHAT - Say pattern (no prepositions)
                return typeof(Say<>);
            }

            return null;
        }

        /// <summary>
        /// Try to create a verb instance using interface detection and CanHandle pattern.
        /// Uses the verb's interfaces to determine what parameters it needs.
        /// </summary>
        private object? TryCreateVerbInstance(Type implementationType, IWord root)
        {
            try
            {
                // Check what interfaces the verb implements to determine parameter resolution
                Type[] interfaces = implementationType.GetInterfaces();
                bool hasFrom = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IFrom<>));
                bool hasTo = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITo<>));
                bool hasWhat = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWhat<>));
                bool hasUsing = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUsing<>));

                System.Diagnostics.Debug.WriteLine($"Creating {implementationType.Name}: hasWhat={hasWhat}, hasFrom={hasFrom}, hasTo={hasTo}, hasUsing={hasUsing}");

                // Create a temporary instance using parameterless constructor to access Resolve methods
                object? tempInstance = Activator.CreateInstance(implementationType);
                if (tempInstance == null)
                {
                    System.Diagnostics.Debug.WriteLine("    Failed to create temp instance with parameterless constructor");
                    return null;
                }

                // Find the actual constructor parameters based on verb interfaces
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

                    // FROM is optional if the keyword isn't in the sentence (fromKeyword == null)
                    // FROM is required if the keyword is present but has no value (fromParam == null && fromKeyword != null)
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

                // Create the actual instance with resolved parameters
                // We need to find the right constructor because Activator.CreateInstance
                // can't infer types from null parameters
                object? instance = null;

                // Get constructor parameter types
                Type[] paramTypes = new Type[constructorParams.Count];
                for (int i = 0; i < constructorParams.Count; i++)
                {
                    if (constructorParams[i] != null)
                    {
                        paramTypes[i] = constructorParams[i]!.GetType();
                    }
                    else
                    {
                        // For null parameters, we need to infer the type from the verb's interfaces
                        // This is complex, so let's try all constructors
                        paramTypes[i] = typeof(object); // Placeholder
                    }
                }

                // Try to find a matching constructor (exclude parameterless constructor - that's only for discovery)
                ConstructorInfo[] constructors = implementationType.GetConstructors();

                foreach (ConstructorInfo ctor in constructors)
                {
                    ParameterInfo[] ctorParams = ctor.GetParameters();

                    // Skip parameterless constructor - it's only for WordFactory discovery
                    if (ctorParams.Length == 0)
                    {
                        continue;
                    }

                    if (ctorParams.Length == constructorParams.Count)
                    {
                        // Check if parameter types are compatible
                        bool compatible = true;
                        for (int i = 0; i < ctorParams.Length; i++)
                        {
                            if (constructorParams[i] != null)
                            {
                                if (!ctorParams[i].ParameterType.IsAssignableFrom(constructorParams[i]!.GetType()))
                                {
                                    compatible = false;
                                    break;
                                }
                            }
                            // null is compatible with any reference type or nullable value type
                        }

                        if (compatible)
                        {
                            try
                            {
                                instance = ctor.Invoke(constructorParams.ToArray());
                                break;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"    Constructor invocation failed: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }

                if (instance == null)
                {
                    System.Diagnostics.Debug.WriteLine("    Failed to create instance with resolved parameters");
                    return null;
                }

                // Validate using CanHandle method if it exists (it's on concrete verb classes, not interface)
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
            // Determine data flow direction based on verb semantics:
            // **Retrieval verbs** (GET, LOAD, DOWNLOAD): WHAT is OUTPUT (where to store retrieved data)
            //   - Pattern: "GET [result] FROM source" - [result] is target for storing data
            //   - Pattern: "DOWNLOAD [file] FROM url TO path" - [file] is target for storing data
            //   - Variables in WHAT should NOT be resolved (they don't exist yet)
            //
            // **Action verbs** (DELETE, SAVE, POST, SEND, SAY): WHAT is INPUT (data to act upon)
            //   - Pattern: "DELETE [filepath]" - [filepath] is input value
            //   - Pattern: "SAVE [data] TO file" - [data] is input value
            //   - Variables in WHAT SHOULD be resolved (they must exist)
            //
            // Detection: Check if verb is Get, Load, or Download base class
            Type? baseType = implementationType.BaseType;
            bool isRetrievalVerb = baseType != null &&
                (baseType.Name.StartsWith("Get") || baseType.Name.StartsWith("Load") || baseType.Name.StartsWith("Download"));

            System.Diagnostics.Debug.WriteLine($"    Verb base type: {baseType?.Name}, isRetrievalVerb: {isRetrievalVerb}");

            // Get all words after the verb until we hit a preposition or end
            List<string> parts = new();
            IWord? currentWord = root.Next;

            while (currentWord != null && !(currentWord is From || currentWord is To || currentWord is Using))
            {
                // For retrieval verbs, if WHAT is a variable, don't resolve it - it's an output target
                if (isRetrievalVerb && currentWord is Words.VariableWord varWord)
                {
                    // Keep variable name without brackets for later resolution
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

            string whatString = string.Join(" ", parts);
            System.Diagnostics.Debug.WriteLine($"    Resolved WHAT: '{whatString}' (isRetrievalVerb={isRetrievalVerb})");

            // Try to call ResolveWhat method if it exists on the verb
            MethodInfo? resolveMethod = implementationType.GetMethod("ResolveWhat");
            if (resolveMethod != null)
            {
                return resolveMethod.Invoke(tempInstance, new object[] { whatString });
            }

            // Default: return the string itself
            return whatString;
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
            // This allows verbs to handle special word types differently
            if (valueWord is ReferenceWord refWord)
            {
                // Try Resolve(ReferenceWord) overload
                MethodInfo? refResolveMethod = implementationType.GetMethod(resolveMethodName, new[] { typeof(ReferenceWord) });
                if (refResolveMethod != null)
                {
                    object? result = refResolveMethod.Invoke(tempInstance, new object[] { refWord });
                    System.Diagnostics.Debug.WriteLine($"    {resolveMethodName}(ReferenceWord) result: {result?.GetType().Name ?? "null"}");
                    return result;
                }
            }

            if (valueWord is VariableWord varWord)
            {
                // Try Resolve(VariableWord) overload
                MethodInfo? varResolveMethod = implementationType.GetMethod(resolveMethodName, new[] { typeof(VariableWord) });
                if (varResolveMethod != null)
                {
                    object? result = varResolveMethod.Invoke(tempInstance, new object[] { varWord });
                    System.Diagnostics.Debug.WriteLine($"    {resolveMethodName}(VariableWord) result: {result?.GetType().Name ?? "null"}");
                    return result;
                }
            }

            // Fallback: resolve to string and call Resolve(string)
            object? valueObj = ResolveWordValue(valueWord);
            string valueString = valueObj?.ToString() ?? "";
            System.Diagnostics.Debug.WriteLine($"    Resolved {resolveMethodName} input: '{valueString}'");

            // Try to call the resolve method (Resolve, ResolveTo, ResolveUsing)
            MethodInfo? resolveMethod = implementationType.GetMethod(resolveMethodName, new[] { typeof(string) });
            if (resolveMethod != null)
            {
                object? result = resolveMethod.Invoke(tempInstance, new object[] { valueString });
                System.Diagnostics.Debug.WriteLine($"    {resolveMethodName} result: {result?.GetType().Name ?? "null"}");
                return result;
            }

            // Fallback: return the string value
            return valueString;
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