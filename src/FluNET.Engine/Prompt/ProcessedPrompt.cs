using System.Text;

namespace FluNET.Prompt;

/// <summary>
/// A lexed and minimally parsed FluNET prompt. The lexer is quote-aware, keeps
/// values inside braces/brackets together, and reports malformed input instead
/// of silently guessing.
/// </summary>
public sealed class ProcessedPrompt
{
    private readonly string _prompt;

    public ProcessedPrompt(string prompt, PromptGrammar? grammar = null)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        Grammar = grammar ?? PromptGrammar.Standard;

        var (tokens, diagnostics) = Tokenize(prompt);
        LexicalTokens = tokens.ToArray();
        Tokens = tokens.Select(token => token.Text).ToArray();
        Syntax = BuildSyntax(tokens, diagnostics, Grammar);
        Diagnostics = diagnostics.ToArray();
    }

    /// <summary>The exact source text used to create this immutable snapshot.</summary>
    public string SourceText => _prompt;

    public PromptGrammar Grammar { get; }

    /// <summary>Compatibility view containing token text only.</summary>
    public string[] Tokens { get; }

    public IReadOnlyList<PromptToken> LexicalTokens { get; }

    public IReadOnlyList<PromptDiagnostic> Diagnostics { get; }

    public PromptSyntax Syntax { get; }

    public bool IsValid => Diagnostics.Count == 0;

    /// <summary>Parses the same source using a host-provided language grammar.</summary>
    public ProcessedPrompt WithGrammar(PromptGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(grammar);
        return ReferenceEquals(Grammar, grammar) ? this : new ProcessedPrompt(_prompt, grammar);
    }

    private static (IReadOnlyList<PromptToken> Tokens, List<PromptDiagnostic> Diagnostics) Tokenize(string input)
    {
        List<PromptToken> tokens = [];
        List<PromptDiagnostic> diagnostics = [];
        StringBuilder current = new();
        Stack<(char Opening, int Position)> delimiters = new();
        char? quote = null;
        bool escaped = false;
        int tokenStart = -1;

        void FlushToken(int endExclusive)
        {
            if (current.Length == 0)
            {
                return;
            }

            string text = current.ToString();
            PromptTokenKind kind = Classify(text);
            tokens.Add(new PromptToken(text, kind, tokenStart, endExclusive - tokenStart));
            current.Clear();
            tokenStart = -1;
        }

        for (int index = 0; index < input.Length; index++)
        {
            char character = input[index];

            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }

            if (quote is not null)
            {
                current.Append(character);
                if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == quote)
                {
                    quote = null;
                }
                continue;
            }

            if (character is '\"' or '\'' && (current.Length == 0 || delimiters.Count > 0))
            {
                if (tokenStart < 0)
                {
                    tokenStart = index;
                }
                quote = character;
                current.Append(character);
                continue;
            }

            if (character is '{' or '[')
            {
                if (tokenStart < 0)
                {
                    tokenStart = index;
                }
                delimiters.Push((character, index));
                current.Append(character);
                continue;
            }

            if (character is '}' or ']')
            {
                char expected = character == '}' ? '{' : '[';
                if (delimiters.Count == 0 || delimiters.Peek().Opening != expected)
                {
                    diagnostics.Add(new PromptDiagnostic(
                        "FLN001",
                        $"Unexpected closing delimiter '{character}'.",
                        index));
                }
                else
                {
                    delimiters.Pop();
                }

                if (tokenStart < 0)
                {
                    tokenStart = index;
                }
                current.Append(character);
                continue;
            }

            if (delimiters.Count == 0 && char.IsWhiteSpace(character))
            {
                FlushToken(index);
                continue;
            }

            if (delimiters.Count == 0 && character is '.' or '?' or '!')
            {
                bool endsLexeme = index == input.Length - 1 || char.IsWhiteSpace(input[index + 1]);
                bool structuredValue = current.Length >= 2 &&
                    ((current[0] == '{' && current[^1] == '}') ||
                     (current[0] == '[' && current[^1] == ']'));
                bool isTerminator = endsLexeme &&
                    (character == '.' || current.Length == 0 || structuredValue);
                if (isTerminator)
                {
                    FlushToken(index);
                    tokens.Add(new PromptToken(character.ToString(), PromptTokenKind.Terminator, index, 1));
                    continue;
                }
            }

            if (tokenStart < 0)
            {
                tokenStart = index;
            }
            current.Append(character);
        }

        FlushToken(input.Length);

        if (quote is not null)
        {
            diagnostics.Add(new PromptDiagnostic(
                "FLN002",
                $"Unterminated {quote} quoted value.",
                tokenStart < 0 ? input.Length : tokenStart));
        }

        foreach ((char opening, int position) in delimiters.Reverse())
        {
            diagnostics.Add(new PromptDiagnostic(
                "FLN003",
                $"Unclosed delimiter '{opening}'.",
                position));
        }

        return (tokens, diagnostics);
    }

    private static PromptTokenKind Classify(string text)
    {
        if (text.Length >= 2 && text[0] == '[' && text[^1] == ']')
        {
            return PromptTokenKind.Variable;
        }

        if (text.Length >= 2 && text[0] == '{' && text[^1] == '}')
        {
            return PromptTokenKind.Reference;
        }

        return PromptTokenKind.Word;
    }

    private static PromptSyntax BuildSyntax(
        IReadOnlyList<PromptToken> tokens,
        ICollection<PromptDiagnostic> diagnostics,
        PromptGrammar grammar)
    {
        List<CommandSyntax> commands = [];
        List<CommandLinkSyntax> links = [];
        List<PromptToken> commandTokens = [];
        PromptToken? pendingConnector = null;
        CommandLinkKind pendingLinkKind = default;

        foreach (PromptToken token in tokens)
        {
            if (token.Kind == PromptTokenKind.Terminator)
            {
                continue;
            }

            if (token.Kind == PromptTokenKind.Word &&
                grammar.TryGetLinkKind(token.Text, out CommandLinkKind linkKind))
            {
                if (commandTokens.Count == 0)
                {
                    diagnostics.Add(new PromptDiagnostic(
                        "FLN004",
                        $"{token.Text.ToUpperInvariant()} must separate two non-empty commands.",
                        token.Start));
                }
                else
                {
                    commands.Add(new CommandSyntax(commandTokens.ToArray(), grammar));
                    commandTokens.Clear();
                    pendingConnector = token;
                    pendingLinkKind = linkKind;
                }
                continue;
            }

            if (pendingConnector is not null && commandTokens.Count == 0 && commands.Count > 0)
            {
                links.Add(new CommandLinkSyntax(
                    commands.Count - 1,
                    commands.Count,
                    pendingLinkKind,
                    pendingConnector));
                pendingConnector = null;
            }

            commandTokens.Add(token);
        }

        if (commandTokens.Count > 0)
        {
            commands.Add(new CommandSyntax(commandTokens.ToArray(), grammar));
        }
        else if (pendingConnector is not null)
        {
            diagnostics.Add(new PromptDiagnostic(
                "FLN004",
                $"{pendingConnector.Text.ToUpperInvariant()} must be followed by a command.",
                pendingConnector.Start));
        }

        return new PromptSyntax(commands, links);
    }

    public override string ToString()
    {
        StringBuilder normalized = new();
        foreach (PromptToken token in LexicalTokens)
        {
            if (token.Kind == PromptTokenKind.Terminator)
            {
                normalized.Append(token.Text);
            }
            else
            {
                if (normalized.Length > 0 && !char.IsWhiteSpace(normalized[^1]))
                {
                    normalized.Append(' ');
                }
                normalized.Append(token.Text);
            }
        }
        return normalized.ToString();
    }
}
