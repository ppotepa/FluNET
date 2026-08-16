using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Variables;

namespace FluNET.Words;

public class WordFactory
{
    private readonly DiscoveryService _discoveryService;

    public WordFactory(DiscoveryService discoveryService)
    {
        _discoveryService = discoveryService;
    }

    /// <summary>
    /// Converts a token into the Classic word model. Keyword lookup is O(1) through
    /// LanguageRegistry; assembly scanning is confined to registry initialization.
    /// </summary>
    public IWord? CreateWord(Token token)
    {
        if (token.Type == TokenType.Variable || VariableResolver.IsVariableReference(token.Value))
            return new VariableWord(token.Value);

        if (token.Type == TokenType.Reference || (token.Value.StartsWith('{') && token.Value.EndsWith('}')))
            return new ReferenceWord(token.Value);

        if (_discoveryService.Registry.IsQualifier(token.Value))
            return new QualifierWord(token.Value);

        if (_discoveryService.Registry.TryCreateWord(token.Value, out IWord? word))
            return word;

        return new LiteralWord(token.Value);
    }

    public bool TryCreateWord(Token token, out IWord? word)
    {
        word = CreateWord(token);
        return word != null;
    }
}

/// <summary>Placeholder for a [variable] reference resolved during binding/execution.</summary>
public class VariableWord : IWord, IKeyword
{
    public VariableWord(string variableReference) => VariableReference = variableReference;

    public string VariableReference { get; }
    public string Text => VariableReference;
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }

    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
    {
        if (Previous != null)
        {
            bool requiresFrom = Previous.GetType().GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IFrom"));

            if (requiresFrom && nextWord is not Keywords.From)
            {
                bool isTerminator = nextWord is LiteralWord literal &&
                    (string.IsNullOrWhiteSpace(literal.Value) || literal.Value is "." or "?" or "!");

                return ValidationResult.Failure(
                    $"{Previous.GetType().Name.ToUpper()} [variable] must be followed by FROM keyword." +
                    (isTerminator ? " Sentence cannot end here." : string.Empty));
            }
        }

        return ValidationResult.Success();
    }

    public bool Validate(IWord word) => true;
}

/// <summary>External resource reference written as {reference}.</summary>
public class ReferenceWord : IWord, IKeyword
{
    public ReferenceWord(string reference) => Reference = reference.Trim('{', '}', ' ');

    public string Reference { get; }
    public string Text => $"{{{Reference}}}";
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }

    public T? ResolveAs<T>() where T : class
    {
        if (typeof(T) == typeof(FileInfo)) return new FileInfo(Reference) as T;
        if (typeof(T) == typeof(Uri)) return Uri.TryCreate(Reference, UriKind.Absolute, out Uri? uri) ? uri as T : null;
        if (typeof(T) == typeof(string)) return Reference as T;
        return null;
    }

    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
    {
        if (Previous != null)
        {
            bool requiresFrom = Previous.GetType().GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IFrom"));

            if (requiresFrom && nextWord is not Keywords.From)
            {
                bool isTerminator = nextWord is LiteralWord literal &&
                    (string.IsNullOrWhiteSpace(literal.Value) || literal.Value is "." or "?" or "!");

                return ValidationResult.Failure(
                    $"{Previous.GetType().Name.ToUpper()} {{reference}} must be followed by FROM keyword." +
                    (isTerminator ? " Sentence cannot end here." : string.Empty));
            }
        }

        return ValidationResult.Success();
    }

    public bool Validate(IWord word) => true;
    public override string ToString() => $"ReferenceWord: {{{Reference}}}";
}

/// <summary>Extensible data-format/type qualifier such as TEXT, JSON or BINARY.</summary>
public class QualifierWord : IWord, IKeyword
{
    public QualifierWord(string qualifier) => Qualifier = qualifier.ToUpperInvariant();

    public string Qualifier { get; }
    public string Text => Qualifier;
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }

    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
    {
        if (Previous != null)
        {
            bool requiresFrom = Previous.GetType().GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.Contains("IFrom"));

            if (requiresFrom)
            {
                bool isTerminator = nextWord is LiteralWord literal &&
                    (string.IsNullOrWhiteSpace(literal.Value) || literal.Value is "." or "?" or "!");
                bool isValidWhat = nextWord is VariableWord || nextWord is ReferenceWord;

                if (!isValidWhat || isTerminator)
                    return ValidationResult.Failure(
                        $"{Previous.GetType().Name.ToUpper()} {Qualifier} must be followed by [variable] or {{reference}}." +
                        (isTerminator ? " Sentence cannot end here." : string.Empty));
            }
        }

        return ValidationResult.Success();
    }

    public bool Validate(IWord word) => true;
    public override string ToString() => $"QualifierWord: {Qualifier}";
}

public class LiteralWord : IWord, IValidatable
{
    public LiteralWord(string value) => Value = value;

    public string Value { get; }
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }
    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon) => ValidationResult.Success();
    public bool Validate(IWord word) => true;
}
