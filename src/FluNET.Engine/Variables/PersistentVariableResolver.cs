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
        private static readonly Dictionary<string, object> _persistentVariables = new(StringComparer.OrdinalIgnoreCase);

        public void Register<T>(string name, T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _persistentVariables[name.ToUpperInvariant()] = value;
        }

        public bool IsRegistered(string name)
        {
            return _persistentVariables.ContainsKey(name.ToUpperInvariant());
        }

        public T? Resolve<T>(string tokenValue) =>
            TryResolve(tokenValue, out T? value) ? value : default;

        public bool TryResolve<T>(string tokenValue, out T? value)
        {
            value = default;

            if (IsSimpleVariable(tokenValue, out string? varName))
            {
                if (!_persistentVariables.TryGetValue(varName!.ToUpperInvariant(), out object? stored))
                {
                    return false;
                }

                if (stored is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                return false;
            }

            if (IsJsonObject(tokenValue, out string? jsonProps) && jsonProps is not null)
            {
                try
                {
                    value = JsonSerializer.Deserialize<T>(jsonProps);
                    return true;
                }
                catch (JsonException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    return false;
                }
            }

            return false;
        }

        public void Clear()
        {
            _persistentVariables.Clear();
        }

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
                jsonProps = "{" + match.Groups[1].Value + "}";
                return true;
            }

            jsonProps = null;
            return false;
        }
    }
}
