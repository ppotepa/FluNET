using FluNET.Language;
using FluNET.Matching;
using System.Text.Json;

namespace FluNET.Variables
{
    /// <summary>
    /// Variable resolver backed by the typed language store.
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
            {
                throw new ArgumentNullException(nameof(value));
            }

            TypeSymbol type;
            try
            {
                type = _language.Types.Get(value.GetType());
            }
            catch (LanguageDefinitionException)
            {
                // Unknown host objects are represented by the language's
                // structural object value.
                type = _language.Types.Object;
            }
            VariableSymbol symbol = new(NormalizeName(name), type, -1);
            _store.Set(symbol, value, VariableScopeKind.Workflow);
        }

        public bool IsRegistered(string name) =>
            _store.TryGet(NormalizeName(name), out _);

        public T? Resolve<T>(string tokenValue) =>
            TryResolve(tokenValue, out T? value) ? value : default;

        public bool TryResolve<T>(string tokenValue, out T? value)
        {
            value = default;

            if (IsSimpleVariable(tokenValue, out string? varName))
            {
                if (!_store.TryGet(varName!, out RuntimeValue? runtime) || runtime is null)
                {
                    return false;
                }

                if (runtime.Value is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                if (IsNumericType(typeof(T)) && runtime.Value is IConvertible)
                {
                    return TryConvertValue(runtime.Value, out value);
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

        public static bool IsVariableReference(string tokenValue) =>
            tokenValue.StartsWith('[') &&
            tokenValue.EndsWith(']') &&
            tokenValue.Length > 2;

        private bool IsSimpleVariable(string token, out string? varName)
        {
            var variableMatcher = _matcherResolver.GetMatcher<IVariableMatcher>();
            if (variableMatcher.IsMatch(token))
            {
                string extracted = variableMatcher.Extract(token);
                if (!extracted.Contains('{') && !extracted.Contains('}') &&
                    extracted == extracted.Trim())
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
                if (destructuringMatcher.IsMatch(extracted))
                {
                    string innerContent = destructuringMatcher.Extract(extracted);
                    jsonProps = "{" + innerContent + "}";
                    return true;
                }
            }

            jsonProps = null;
            return false;
        }

        public void Clear() => _store.Clear();

        public IEnumerable<string> GetVariableNames() => _store.Snapshot().Keys;

        private static string NormalizeName(string name) =>
            string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("A variable name is required.", nameof(name))
                : name.Trim().TrimStart('[').TrimEnd(']');

        private static bool TryConvertValue<T>(object source, out T? value)
        {
            try
            {
                value = (T?)Convert.ChangeType(
                    source,
                    typeof(T),
                    System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (InvalidCastException)
            {
            }
            catch (FormatException)
            {
            }
            catch (OverflowException)
            {
            }

            value = default;
            return false;
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
