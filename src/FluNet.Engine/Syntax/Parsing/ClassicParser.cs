using FluNET.Diagnostics;
using FluNET.Language;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Lexing;

namespace FluNET.Syntax.Parsing;

public sealed record ParseResult(ScriptNode? Script, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Script != null && Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error);
}

public sealed class ClassicParser
{
    private static readonly Dictionary<string, ClauseKind> RoleKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WHAT"] = ClauseKind.What, ["FROM"] = ClauseKind.From, ["TO"] = ClauseKind.To, ["USING"] = ClauseKind.Using, ["WITH"] = ClauseKind.With
    };

    private readonly LanguageSnapshot _language;
    private readonly ClassicLexer _lexer;

    public ClassicParser(LanguageSnapshot language, ClassicLexer? lexer = null) { _language = language; _lexer = lexer ?? new ClassicLexer(); }

    public ParseResult Parse(string source)
    {
        IReadOnlyList<ClassicToken> tokens = _lexer.Lex(source); var diagnostics = new List<Diagnostic>(); var pipelines = new List<PipelineNode>(); int index = 0;
        while (index < tokens.Count)
        {
            SkipNewLines(tokens, ref index); if (index >= tokens.Count) break;
            var sentences = new List<SentenceNode>();
            while (index < tokens.Count && tokens[index].Kind != ClassicTokenKind.NewLine)
            {
                SentenceNode? sentence = ParseSentence(tokens, ref index, diagnostics); if (sentence != null) sentences.Add(sentence);
                if (index < tokens.Count && IsWord(tokens[index], "THEN")) { index++; continue; }
                break;
            }
            if (sentences.Count > 0) { int start = sentences[0].Span?.Start ?? 0; int end = sentences[^1].Span?.End ?? start; pipelines.Add(new PipelineNode(sentences) { Span = new TextSpan(start, Math.Max(0, end - start)) }); }
            SkipNewLines(tokens, ref index);
        }
        return new(new ScriptNode(pipelines), diagnostics);
    }

    private SentenceNode? ParseSentence(IReadOnlyList<ClassicToken> tokens, ref int index, List<Diagnostic> diagnostics)
    {
        if (index >= tokens.Count || tokens[index].Kind != ClassicTokenKind.Word)
        {
            ClassicToken? token = index < tokens.Count ? tokens[index] : null; diagnostics.Add(Diagnostic.Error("FLU1001", "Expected a verb at the start of the sentence.", token?.Span)); SkipUntilBoundary(tokens, ref index); return null;
        }

        ClassicToken verbToken = tokens[index++]; string verb = verbToken.Text.ToUpperInvariant(); string? qualifier = null;
        if (index < tokens.Count && tokens[index].Kind == ClassicTokenKind.Word && _language.IsQualifier(tokens[index].Text)) qualifier = tokens[index++].Text.ToUpperInvariant();

        ClauseKind currentRole = ClauseKind.What; var clauses = new List<ClauseNode>(); int end = verbToken.Span.End;
        while (index < tokens.Count)
        {
            ClassicToken token = tokens[index]; if (token.Kind == ClassicTokenKind.NewLine || IsWord(token, "THEN")) break;
            if (token.Kind == ClassicTokenKind.Word && RoleKeywords.TryGetValue(token.Text, out ClauseKind role)) { currentRole = role; end = token.Span.End; index++; continue; }
            ExpressionNode expression = ToExpression(token); clauses.Add(new ClauseNode(currentRole, expression) { Span = token.Span }); end = token.Span.End; index++;
        }

        return new SentenceNode(verb, clauses) { Qualifier = qualifier, Span = new TextSpan(verbToken.Span.Start, Math.Max(0, end - verbToken.Span.Start)) };
    }

    private static ExpressionNode ToExpression(ClassicToken token)
    {
        ExpressionNode expression = token.Kind switch
        {
            ClassicTokenKind.Variable => ParseVariablePath(token.Text),
            ClassicTokenKind.Reference => new ReferenceExpression(token.Text),
            ClassicTokenKind.String when token.Text.Contains('[') => new InterpolatedStringExpression(token.Text),
            ClassicTokenKind.String => new LiteralExpression(token.Text),
            _ => new LiteralExpression(token.Text)
        };
        return expression with { Span = token.Span };
    }

    private static ExpressionNode ParseVariablePath(string value)
    {
        string[] parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        ExpressionNode current = new VariableExpression(parts[0]);
        for (int i = 1; i < parts.Length; i++) current = new PropertyExpression(current, parts[i]);
        return current;
    }

    private static bool IsWord(ClassicToken token, string text) => token.Kind == ClassicTokenKind.Word && token.Text.Equals(text, StringComparison.OrdinalIgnoreCase);
    private static void SkipNewLines(IReadOnlyList<ClassicToken> tokens, ref int index) { while (index < tokens.Count && tokens[index].Kind == ClassicTokenKind.NewLine) index++; }
    private static void SkipUntilBoundary(IReadOnlyList<ClassicToken> tokens, ref int index) { while (index < tokens.Count && tokens[index].Kind != ClassicTokenKind.NewLine && !IsWord(tokens[index], "THEN")) index++; }
}
