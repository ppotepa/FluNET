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
    Using,
    Marked
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
    public CommandSyntax(IReadOnlyList<PromptToken> tokens, PromptGrammar? grammar = null)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Count == 0)
        {
            throw new ArgumentException("A command must contain a verb.", nameof(tokens));
        }

        PromptToken[] snapshot = tokens.ToArray();
        PromptGrammar activeGrammar = grammar ?? PromptGrammar.Standard;
        Verb = snapshot[0];
        AllTokens = snapshot;
        (PromptToken[] arguments, CommandModifierSyntax[] modifiers) =
            SplitModifiers(snapshot.Skip(1).ToArray(), activeGrammar);
        Tokens = new[] { Verb }.Concat(arguments).ToArray();
        Modifiers = modifiers;
        Clauses = ParseClauses(arguments, activeGrammar);
    }

    public IReadOnlyList<PromptToken> Tokens { get; }
    public IReadOnlyList<PromptToken> AllTokens { get; }
    public PromptToken Verb { get; }
    public IReadOnlyList<ClauseSyntax> Clauses { get; }
    public IReadOnlyList<CommandModifierSyntax> Modifiers { get; }
    public IReadOnlyList<PromptToken> Arguments => Tokens.Skip(1).ToArray();
    public SourceSpan Span => SourceSpan.FromBounds(Verb.Start, AllTokens[^1].Span.End);

    private static (PromptToken[] Arguments, CommandModifierSyntax[] Modifiers) SplitModifiers(
        IReadOnlyList<PromptToken> arguments,
        PromptGrammar grammar)
    {
        int firstModifier = -1;
        for (int index = 0; index < arguments.Count; index++)
        {
            if (grammar.TryGetModifier(arguments, index, out _, out _))
            {
                firstModifier = index;
                break;
            }
        }
        if (firstModifier < 0)
        {
            return (arguments.ToArray(), Array.Empty<CommandModifierSyntax>());
        }

        List<CommandModifierSyntax> modifiers = [];
        int cursor = firstModifier;
        while (cursor < arguments.Count)
        {
            if (!grammar.TryGetModifier(
                arguments,
                cursor,
                out CommandModifierDescriptor? descriptor,
                out int consumed))
            {
                throw new ArgumentException(
                    $"Unexpected token '{arguments[cursor].Text}' after a command modifier.");
            }

            PromptToken introducer = arguments[cursor];
            PromptToken? name = consumed == 2 ? arguments[cursor + 1] : null;
            cursor += consumed;
            List<PromptToken> values = [];
            while (cursor < arguments.Count &&
                !grammar.TryGetModifier(arguments, cursor, out _, out _))
            {
                values.Add(arguments[cursor++]);
            }
            modifiers.Add(new CommandModifierSyntax(
                descriptor!.Kind,
                introducer,
                name,
                values));
        }

        return (arguments.Take(firstModifier).ToArray(), modifiers.ToArray());
    }

    private static IReadOnlyList<ClauseSyntax> ParseClauses(
        IEnumerable<PromptToken> tokens,
        PromptGrammar grammar)
    {
        List<ClauseSyntax> clauses = [];
        PromptClauseKind kind = PromptClauseKind.Subject;
        PromptToken? keyword = null;
        List<PromptToken> values = [];

        foreach (PromptToken token in tokens)
        {
            PromptClauseKind? nextKind = token.Kind == PromptTokenKind.Word &&
                grammar.TryGetClauseKind(token.Text, out PromptClauseKind clauseKind)
                    ? clauseKind
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

public sealed record CommandModifierSyntax(
    CommandModifierKind Kind,
    PromptToken Introducer,
    PromptToken? Name,
    IReadOnlyList<PromptToken> Values);

/// <summary>The connector between two adjacent command nodes.</summary>
public sealed record CommandLinkSyntax(
    int PredecessorIndex,
    int SuccessorIndex,
    CommandLinkKind Kind,
    PromptToken Connector);

/// <summary>The immutable parsed top-level shape of a prompt.</summary>
public sealed record PromptSyntax
{
    public PromptSyntax(
        IEnumerable<CommandSyntax> commands,
        IEnumerable<CommandLinkSyntax>? links = null)
    {
        Commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
        Links = links?.ToArray() ?? Array.Empty<CommandLinkSyntax>();
    }

    public IReadOnlyList<CommandSyntax> Commands { get; }
    public IReadOnlyList<CommandLinkSyntax> Links { get; }

    public SourceSpan Span => Commands.Count == 0
        ? default
        : SourceSpan.FromBounds(Commands[0].Span.Start, Commands[^1].Span.End);
}

/// <summary>A lexer/parser diagnostic with a stable machine-readable code.</summary>
public sealed record PromptDiagnostic(string Code, string Message, int Position);
