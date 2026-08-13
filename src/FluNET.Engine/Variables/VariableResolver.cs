using FluNET.Language;
using FluNET.Matching;
using System.Text.Json;

namespace FluNET.Variables
{
    /// <summary>
    /// Compatibility variable resolver backed by the typed 0.4 variable store.
    /// </summary>
    public class VariableResolver : IVariableResolver
    {
        private readonly MatcherResolver _matcherResolver;
        private readonly IVariableStore _store;
        private readonly LanguageSnapshot _language;

        public VariableResolver(MatcherResolver matcherResolver)
            : this(
                matcherResolver,
                new VariableStore(StandardLanguage.CreateSnapshot()),
                StandardLanguage.CreateSnapshot())
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

        public void Register<T>(string name, T value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            TypeSymbol type = _language.Types.Get(value.GetType());
            VariableSymbol symbol = new(NormalizeName(name), type, -1);
            _store.Set(symbol, value, VariableScopeKind.Workflow);
        }

        public bool IsRegistered(string name) =>
            _store.TryGet(NormalizeName(name), out _);

        public T? Resolve<T>(string tokenValue)
        {
            if (IsSimpleVariable(tokenValue, out string? varName))
            {
                return _store.TryGet(varName!, out RuntimeValue? runtime) &&
                    runtime!.Value is T typedValue
                    ? typedValue
                    : default;
            }

            if (IsJsonObject(tokenValue, out string? jsonProps) && jsonProps != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(jsonProps);
                }
                catch
                {
                    return default;
                }
            }

            return default;
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
                if (!extracted.Contains('{') && !extracted.Contains('}'))
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
    }
}