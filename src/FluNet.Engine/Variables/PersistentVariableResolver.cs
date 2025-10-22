using System.Text.Json;
using System.Text.RegularExpressions;

namespace FluNET.Variables
{
    /// <summary>
    /// Persistent variable resolver that maintains variables across multiple commands.
    /// Uses a static dictionary to persist variables throughout the application lifetime.
    /// This is ideal for CLI mode where variables should persist between commands.
    /// </summary>
    public class PersistentVariableResolver : IVariableResolver
    {
        // Static dictionary shared across all instances - persists for application lifetime
        private static readonly Dictionary<string, object> _persistentVariables = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a variable with the persistent store.
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

            _persistentVariables[name.ToUpperInvariant()] = value;
        }

        /// <summary>
        /// Checks if a variable is registered in the persistent store
        /// </summary>
        /// <param name="name">The variable name (without brackets)</param>
        /// <returns>True if the variable exists</returns>
        public bool IsRegistered(string name)
        {
            return _persistentVariables.ContainsKey(name.ToUpperInvariant());
        }

        /// <summary>
        /// Resolves a variable reference from the persistent store.
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
                if (_persistentVariables.TryGetValue(varName!.ToUpperInvariant(), out object? value))
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
        /// Clears all variables from the persistent store
        /// </summary>
        public void Clear()
        {
            _persistentVariables.Clear();
        }

        /// <summary>
        /// Gets all registered variable names from the persistent store
        /// </summary>
        public IEnumerable<string> GetVariableNames()
        {
            return _persistentVariables.Keys;
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
    }
}
