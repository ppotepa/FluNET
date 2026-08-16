using FluNET.Language;

namespace FluNET.Syntax.Ast;

/// <summary>
/// Stable immutable syntax model between parsing and binding. Runtime objects are deliberately absent.
/// </summary>
public abstract record SyntaxNode;

public sealed record ScriptNode(IReadOnlyList<PipelineNode> Pipelines) : SyntaxNode;

public sealed record PipelineNode(IReadOnlyList<SentenceNode> Sentences) : SyntaxNode;

public sealed record SentenceNode(
    string Verb,
    IReadOnlyList<ClauseNode> Clauses) : SyntaxNode
{
    public string? Qualifier { get; init; }
}

public sealed record ClauseNode(
    ClauseKind Kind,
    ExpressionNode Value) : SyntaxNode;

public abstract record ExpressionNode : SyntaxNode;

public sealed record LiteralExpression(string Value) : ExpressionNode;
public sealed record VariableExpression(string Name) : ExpressionNode;
public sealed record ReferenceExpression(string Reference) : ExpressionNode;
public sealed record PropertyExpression(ExpressionNode Target, string Property) : ExpressionNode;
public sealed record InterpolatedStringExpression(string Template) : ExpressionNode;
public sealed record PipelineValueExpression : ExpressionNode;
