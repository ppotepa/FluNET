namespace FluNET.Prompt;

/// <summary>A half-open range in the original prompt.</summary>
public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => checked(Start + Length);

    public static SourceSpan FromBounds(int start, int end) =>
        new(start, checked(end - start));
}

/// <summary>Kind of lexical element found in a FluNET prompt.</summary>
public enum PromptTokenKind
{
    Word,
    Variable,
    Reference,
    Terminator
}

/// <summary>A token together with its location in the original prompt.</summary>
public sealed record PromptToken(string Text, PromptTokenKind Kind, int Start, int Length)
{
    public SourceSpan Span => new(Start, Length);
}

public enum PromptClauseKind
{
    Subject,
    From,
    To,
    Using
}

/// <summary>A subject or prepositional clause within a command.</summary>
public sealed record ClauseSyntax
{
    public ClauseSyntax(
        PromptClauseKind kind,
        PromptToken? keyword,
        IEnumerable<PromptToken> values)
    {
        Kind = kind;
        Keyword = keyword;
        Values = values?.ToArray() ?? throw new ArgumentNullException(nameof(values));
    }

    public PromptClauseKind Kind { get; }
    public PromptToken? Keyword { get; }
    public IReadOnlyList<PromptToken> Values { get; }

    public SourceSpan Span
    {
        get
        {
            PromptToken? first = Keyword ?? Values.FirstOrDefault();
            PromptToken? last = Values.LastOrDefault() ?? Keyword;
            return first is null || last is null
                ? default
                : SourceSpan.FromBounds(first.Start, last.Span.End);
        }
    }
}

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

        PromptToken[] snapshot = tokens.ToArray();
        Tokens = snapshot;
        Verb = snapshot[0];
        Clauses = ParseClauses(snapshot.Skip(1));
    }

    public IReadOnlyList<PromptToken> Tokens { get; }
    public PromptToken Verb { get; }
    public IReadOnlyList<ClauseSyntax> Clauses { get; }
    public IReadOnlyList<PromptToken> Arguments => Tokens.Skip(1).ToArray();
    public SourceSpan Span => SourceSpan.FromBounds(Verb.Start, Tokens[^1].Span.End);

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

/// <summary>The immutable parsed top-level shape of a prompt.</summary>
public sealed record PromptSyntax
{
    public PromptSyntax(IEnumerable<CommandSyntax> commands)
    {
        Commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
    }

    public IReadOnlyList<CommandSyntax> Commands { get; }

    public SourceSpan Span => Commands.Count == 0
        ? default
        : SourceSpan.FromBounds(Commands[0].Span.Start, Commands[^1].Span.End);
}

/// <summary>A lexer/parser diagnostic with a stable machine-readable code.</summary>
public sealed record PromptDiagnostic(string Code, string Message, int Position);
