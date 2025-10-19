using System.Text.Json;
using System.Text.RegularExpressions;

namespace FluNET.Variables
{
    /// <summary>
    /// Resolves variable references in FluNET commands.
    /// Handles both simple variables [Name] and JSON object properties [{prop1, prop2}].
    /// </summary>
    public class VariableResolver
    {
        private readonly Dictionary<string, object> _variables = [];

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
            {
                throw new ArgumentNullException(nameof(value));
            }

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
            if (IsJsonObject(tokenValue, out string? jsonProps) && jsonProps != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonProps);
                }
                catch
                {
                    // JSON parsing failed
                    return default;
                }
            }

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

        private bool IsSimpleVariable(string token, out string? varName)
        {
            Match match = Regex.Match(token, @"^\[([A-Za-z0-9_]+)\]$");
            if (match.Success)
            {
                varName = match.Groups[1].Value;
                return true;
            }

            varName = null;
            return false;
        }

        private bool IsJsonObject(string token, out string? jsonProps)
        {
            Match match = Regex.Match(token, @"^\[\{(.+)\}\]$");
            if (match.Success)
            {
                // Format as proper JSON
                jsonProps = "{" + match.Groups[1].Value + "}";
                return true;
            }

            jsonProps = null;
            return false;
        }

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