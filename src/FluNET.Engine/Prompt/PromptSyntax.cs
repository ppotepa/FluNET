namespace FluNET.Prompt;

/// <summary>Kind of lexical element found in a FluNET prompt.</summary>
public enum PromptTokenKind
{
    Word,
    Variable,
    Reference,
    Terminator
}

/// <summary>A token together with its location in the original prompt.</summary>
public sealed record PromptToken(string Text, PromptTokenKind Kind, int Start, int Length);

public enum PromptClauseKind
{
    Subject,
    From,
    To,
    Using
}

/// <summary>A subject or prepositional clause within a command.</summary>
public sealed record ClauseSyntax(
    PromptClauseKind Kind,
    PromptToken? Keyword,
    IReadOnlyList<PromptToken> Values);

/// <summary>A hierarchical command node, excluding the THEN separator.</summary>
public sealed record CommandSyntax
{
    public CommandSyntax(IReadOnlyList<PromptToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Count == 0)
        {
            throw new ArgumentException("A command must contain a verb.", nameof(tokens));
        }

        Tokens = tokens;
        Verb = tokens[0];
        Clauses = ParseClauses(tokens.Skip(1));
    }

    public IReadOnlyList<PromptToken> Tokens { get; }
    public PromptToken Verb { get; }
    public IReadOnlyList<ClauseSyntax> Clauses { get; }
    public IReadOnlyList<PromptToken> Arguments => Tokens.Skip(1).ToArray();

    private static IReadOnlyList<ClauseSyntax> ParseClauses(IEnumerable<PromptToken> tokens)
    {
        List<ClauseSyntax> clauses = [];
        PromptClauseKind kind = PromptClauseKind.Subject;
        PromptToken? keyword = null;
        List<PromptToken> values = [];

        foreach (PromptToken token in tokens)
        {
            PromptClauseKind? nextKind = token.Kind == PromptTokenKind.Word
                ? token.Text.ToUpperInvariant() switch
                {
                    "FROM" => PromptClauseKind.From,
                    "TO" => PromptClauseKind.To,
                    "USING" => PromptClauseKind.Using,
                    _ => null
                }
                : null;

            if (nextKind is null)
            {
                values.Add(token);
                continue;
            }

            clauses.Add(new ClauseSyntax(kind, keyword, values.ToArray()));
            kind = nextKind.Value;
            keyword = token;
            values.Clear();
        }

        clauses.Add(new ClauseSyntax(kind, keyword, values.ToArray()));
        return clauses;
    }
}

/// <summary>The parsed top-level shape of a prompt.</summary>
public sealed record PromptSyntax(IReadOnlyList<CommandSyntax> Commands);

/// <summary>A lexer/parser diagnostic with a stable machine-readable code.</summary>
public sealed record PromptDiagnostic(string Code, string Message, int Position);
