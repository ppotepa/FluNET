<<<<<<< HEAD
using FluNET.Language;
=======
>>>>>>> origin/agent/stabilize-poc-foundation
using FluNET.Matching;
using System.Text.Json;

namespace FluNET.Variables
{
    /// <summary>
<<<<<<< HEAD
    /// Compatibility variable resolver backed by the typed 0.4 variable store.
    /// </summary>
    public class VariableResolver : IVariableResolver
    {
        private readonly MatcherResolver _matcherResolver;
        private readonly IVariableStore _store;
        private readonly LanguageSnapshot _language;

        public VariableResolver()
            : this(new MatcherResolver([]))
        {
        }

        public VariableResolver(MatcherResolver matcherResolver)
            : this(
                matcherResolver,
                new VariableStore(StandardLanguage.CreateSnapshot()),
                StandardLanguage.CreateSnapshot())
        {
        }

        public VariableResolver(
            MatcherResolver matcherResolver,
            LanguageSnapshot language)
            : this(
                matcherResolver,
                new VariableStore(language ?? throw new ArgumentNullException(nameof(language))),
                language)
        {
        }

        public VariableResolver(
            MatcherResolver matcherResolver,
            IVariableStore store,
            LanguageSnapshot language)
        {
            _matcherResolver = matcherResolver ?? throw new ArgumentNullException(nameof(matcherResolver));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _language = language ?? throw new ArgumentNullException(nameof(language));
        }

        public IVariableStore Store => _store;

        public void Register<T>(string name, T value)
        {
            if (value is null)
=======
    /// Default implementation of variable resolver.
    /// Maintains variables in memory for a single scope (transient/scoped DI lifetime).
    /// Handles both simple variables [Name] and JSON object properties [{prop1, prop2}].
    /// </summary>
    public class VariableResolver : IVariableResolver
    {
        private readonly Dictionary<string, object> _variables = [];
        private readonly MatcherResolver _matcherResolver;

        public VariableResolver(MatcherResolver matcherResolver)
        {
            _matcherResolver = matcherResolver ?? throw new ArgumentNullException(nameof(matcherResolver));
        }

        /// <summary>
        /// Registers a variable with the resolver.
        /// Variable names are case-insensitive.
        /// </summary>
        /// <typeparam name="T">The type of the variable value</typeparam>
        /// <param name="name">The variable name (without brackets)</param>
        /// <param name="value">The value to store</param>
        public void Register<T>(string name, T value)
        {
            if (value == null)
>>>>>>> origin/agent/stabilize-poc-foundation
            {
                throw new ArgumentNullException(nameof(value));
            }

<<<<<<< HEAD
            TypeSymbol type;
            try
            {
                type = _language.Types.Get(value.GetType());
            }
            catch (LanguageDefinitionException)
            {
                // Compatibility hosts may register arbitrary object graphs.
                // Keep them as the language's structural object value.
                type = _language.Types.Object;
            }
            VariableSymbol symbol = new(NormalizeName(name), type, -1);
            _store.Set(symbol, value, VariableScopeKind.Workflow);
        }

        public bool IsRegistered(string name) =>
            _store.TryGet(NormalizeName(name), out _);

        public T? Resolve<T>(string tokenValue)
        {
            if (IsSimpleVariable(tokenValue, out string? varName))
            {
                if (!_store.TryGet(varName!, out RuntimeValue? runtime) || runtime is null)
                {
                    return default;
                }
                return runtime.Value is T typedValue
                    ? typedValue
                    : IsNumericType(typeof(T)) && runtime.Value is IConvertible
                        ? ConvertValue<T>(runtime.Value)
                        : default;
            }

=======
            _variables[name.ToUpperInvariant()] = value;
        }

        /// <summary>
        /// Checks if a variable is registered
        /// </summary>
        /// <param name="name">The variable name (without brackets)</param>
        /// <returns>True if the variable exists</returns>
        public bool IsRegistered(string name)
        {
            return _variables.ContainsKey(name.ToUpperInvariant());
        }

        /// <summary>
        /// Resolves a variable reference from a token value.
        /// Supports:
        /// - Simple variables: [Data] -> resolves to registered variable "Data"
        /// - JSON objects: [{name, surname}] -> creates object with those properties
        /// Returns null if the variable cannot be resolved or doesn't match the expected type.
        /// </summary>
        /// <typeparam name="T">The expected type of the variable</typeparam>
        /// <param name="tokenValue">The token value (e.g. [Data] or [{name, surname}])</param>
        /// <returns>The resolved variable value, or null if not found or type mismatch</returns>
        public T? Resolve<T>(string tokenValue)
        {
            // Check if it's a simple variable [Name]
            if (IsSimpleVariable(tokenValue, out string? varName))
            {
                if (_variables.TryGetValue(varName!.ToUpperInvariant(), out object? value))
                {
                    // First try direct cast
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }

                    // Don't try conversion - just return null for type mismatch
                    return default;
                }

                // Variable not found
                return default;
            }

            // Check if it's a JSON object [{prop1, prop2}]
>>>>>>> origin/agent/stabilize-poc-foundation
            if (IsJsonObject(tokenValue, out string? jsonProps) && jsonProps != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonProps);
                }
                catch
                {
<<<<<<< HEAD
=======
                    // JSON parsing failed
>>>>>>> origin/agent/stabilize-poc-foundation
                    return default;
                }
            }

<<<<<<< HEAD
            return default;
        }

        public static bool IsVariableReference(string tokenValue) =>
            tokenValue.StartsWith('[') &&
            tokenValue.EndsWith(']') &&
            tokenValue.Length > 2;
=======
            // Not a valid variable reference
            return default;
        }

        /// <summary>
        /// Checks if a token value is a variable reference (starts with [ and ends with ])
        /// Must contain at least one character between the brackets.
        /// </summary>
        public static bool IsVariableReference(string tokenValue)
        {
            return tokenValue.StartsWith('[') &&
                   tokenValue.EndsWith(']') &&
                   tokenValue.Length > 2; // Must have content between brackets
        }
>>>>>>> origin/agent/stabilize-poc-foundation

        private bool IsSimpleVariable(string token, out string? varName)
        {
            var variableMatcher = _matcherResolver.GetMatcher<IVariableMatcher>();
            if (variableMatcher.IsMatch(token))
            {
                string extracted = variableMatcher.Extract(token);
<<<<<<< HEAD
                if (!extracted.Contains('{') && !extracted.Contains('}') &&
                    extracted == extracted.Trim())
=======
                // Check if it's NOT a destructuring pattern (no { })
                if (!extracted.Contains('{') && !extracted.Contains('}'))
>>>>>>> origin/agent/stabilize-poc-foundation
                {
                    varName = extracted;
                    return true;
                }
            }

            varName = null;
            return false;
        }

        private bool IsJsonObject(string token, out string? jsonProps)
        {
            var variableMatcher = _matcherResolver.GetMatcher<IVariableMatcher>();
            if (variableMatcher.IsMatch(token))
            {
                string extracted = variableMatcher.Extract(token);
                var destructuringMatcher = _matcherResolver.GetMatcher<IDestructuringMatcher>();
<<<<<<< HEAD
                if (destructuringMatcher.IsMatch(extracted))
                {
=======

                if (destructuringMatcher.IsMatch(extracted))
                {
                    // Format as proper JSON
>>>>>>> origin/agent/stabilize-poc-foundation
                    string innerContent = destructuringMatcher.Extract(extracted);
                    jsonProps = "{" + innerContent + "}";
                    return true;
                }
            }

            jsonProps = null;
            return false;
        }

<<<<<<< HEAD
        public void Clear() => _store.Clear();

        public IEnumerable<string> GetVariableNames() => _store.Snapshot().Keys;

        private static string NormalizeName(string name) =>
            string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("A variable name is required.", nameof(name))
                : name.Trim().TrimStart('[').TrimEnd(']');

        private static T? ConvertValue<T>(object value)
        {
            try
            {
                return (T?)Convert.ChangeType(
                    value,
                    typeof(T),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (
                exception is InvalidCastException or FormatException or OverflowException)
            {
                return default;
            }
        }

        private static bool IsNumericType(Type type) =>
            type == typeof(byte) || type == typeof(sbyte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) ||
            type == typeof(decimal);
    }
}
=======
        /// <summary>
        /// Clears all registered variables
        /// </summary>
        public void Clear()
        {
            _variables.Clear();
        }

        /// <summary>
        /// Gets all registered variable names
        /// </summary>
        public IEnumerable<string> GetVariableNames()
        {
            return _variables.Keys;
        }
    }
}
>>>>>>> origin/agent/stabilize-poc-foundation
