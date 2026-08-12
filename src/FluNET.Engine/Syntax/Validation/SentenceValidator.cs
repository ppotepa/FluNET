using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Words;

namespace FluNET.Syntax.Validation;

/// <summary>Validates every command in a prompt before execution.</summary>
public sealed class SentenceValidator(Lexicon.Lexicon lexicon, WordFactory wordFactory)
{
    /// <summary>
    /// Validates command trees whose boundaries were established by the parser.
    /// This is the canonical program-validation path.
    /// </summary>
    public ValidationResult ValidateCommands(IReadOnlyList<TokenTree> commandTrees)
    {
        ArgumentNullException.ThrowIfNull(commandTrees);
        if (commandTrees.Count == 0)
        {
            return ValidationResult.Failure("Empty sentence.");
        }

        foreach (TokenTree tree in commandTrees)
        {
            ValidationResult result = ValidateCommand(tree.GetTokens().ToArray());
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Compatibility entry point for callers that still provide one flattened
    /// token tree. The engine pipeline uses <see cref="ValidateCommands"/>.
    /// </summary>
    public ValidationResult ValidateSentence(TokenTree tokenTree)
    {
        if (tokenTree.Count == 0)
        {
            return ValidationResult.Failure("Empty sentence.");
        }

        List<List<Token>> commands = SplitCommands(tokenTree);
        if (commands.Count == 0)
        {
            return ValidationResult.Failure("Empty sentence.");
        }

        foreach (List<Token> command in commands)
        {
            ValidationResult result = ValidateCommand(command);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Success();
    }

    private ValidationResult ValidateCommand(IReadOnlyList<Token> tokens)
    {
        if (tokens.Count == 0)
        {
            return ValidationResult.Failure("THEN must separate two non-empty commands.");
        }

        Token? emptyStructuredValue = tokens.FirstOrDefault(token =>
            token.Type is TokenType.Variable or TokenType.Reference && token.Value.Length == 2);
        if (emptyStructuredValue is not null)
        {
            return ValidationResult.Failure(
                $"Structured value '{emptyStructuredValue.Value}' cannot be empty.");
        }

        List<IWord> words = tokens.Select(wordFactory.CreateWord).OfType<IWord>().ToList();
        if (words.Count == 0)
        {
            return ValidationResult.Failure("Empty command.");
        }

        if (words[0] is not IVerb verb)
        {
            return ValidationResult.Failure($"Sentence must start with a known verb, got '{tokens[0].Value}'.");
        }

        for (int index = 1; index < words.Count; index++)
        {
            IWord previous = words[index - 1];
            IWord current = words[index];
            previous.Next = current;
            current.Previous = previous;

            if (previous is IValidatable validatable)
            {
                ValidationResult pairResult = validatable.ValidateNext(current, lexicon);
                if (!pairResult.IsValid)
                {
                    return pairResult;
                }
            }
        }

        ValidationResult shape = ValidateShape(verb, words);
        if (!shape.IsValid)
        {
            return shape;
        }

        if (verb.Text.Equals("DOWNLOAD", StringComparison.OrdinalIgnoreCase))
        {
            int fromIndex = words.FindIndex(word => word is From);
            if (fromIndex >= 0 && fromIndex + 1 < words.Count && words[fromIndex + 1] is not VariableWord)
            {
                string source = words[fromIndex + 1] switch
                {
                    ReferenceWord reference => reference.Reference,
                    LiteralWord literal => literal.Value,
                    _ => string.Empty
                };
                if (!Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) ||
                    uri.Scheme is not ("http" or "https"))
                {
                    return ValidationResult.Failure(
                        "DOWNLOAD FROM requires an absolute HTTP or HTTPS URL.");
                }
            }
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateShape(IVerb verb, IReadOnlyList<IWord> words)
    {
        string name = verb.Text.ToUpperInvariant();
        if (words.Count < 2)
        {
            return ValidationResult.Failure($"{name} requires a subject.");
        }

        return name switch
        {
            "GET" or "FETCH" or "RETRIEVE" or "LOAD" or "DOWNLOAD" =>
                RequirePreposition(name, words, typeof(From), "FROM"),
            "SAVE" or "POST" or "SEND" =>
                RequirePreposition(name, words, typeof(To), "TO"),
            "TRANSFORM" =>
                RequirePreposition(name, words, typeof(Using), "USING"),
            _ => ValidationResult.Success()
        };
    }

    private static ValidationResult RequirePreposition(
        string verb,
        IReadOnlyList<IWord> words,
        Type prepositionType,
        string prepositionName)
    {
        int prepositionIndex = -1;
        for (int index = 1; index < words.Count; index++)
        {
            if (prepositionType.IsInstanceOfType(words[index]))
            {
                prepositionIndex = index;
                break;
            }
        }

        if (prepositionIndex < 0)
        {
            return ValidationResult.Failure($"{verb} requires the {prepositionName} clause.");
        }

        if (prepositionIndex == 1)
        {
            return ValidationResult.Failure($"{verb} requires a subject before {prepositionName}.");
        }

        if (words[1] is QualifierWord && prepositionIndex == 2 && verb != "LOAD")
        {
            return ValidationResult.Failure(
                $"{verb} requires a subject after the qualifier and before {prepositionName}.");
        }

        if (prepositionIndex == words.Count - 1)
        {
            return ValidationResult.Failure($"{prepositionName} requires a value.");
        }

        return ValidationResult.Success();
    }

    private static List<List<Token>> SplitCommands(TokenTree tree)
    {
        List<List<Token>> commands = [];
        List<Token> current = [];

        foreach (Token token in tree.GetTokens())
        {
            if (token.Value.Equals("THEN", StringComparison.OrdinalIgnoreCase))
            {
                commands.Add(current);
                current = [];
            }
            else
            {
                current.Add(token);
            }
        }

        commands.Add(current);
        return commands;
    }
}
