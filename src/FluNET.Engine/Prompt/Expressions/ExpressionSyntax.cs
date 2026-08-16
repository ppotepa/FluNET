namespace FluNET.Prompt.Expressions;

public abstract record ExpressionSyntax(SourceSpan Span);

public sealed record LiteralExpressionSyntax(
    string Text,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record VariableExpressionSyntax(
    string Name,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record UnaryExpressionSyntax(
    string Operator,
    ExpressionSyntax Operand,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record BinaryExpressionSyntax(
    ExpressionSyntax Left,
    string Operator,
    ExpressionSyntax Right,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record ParenthesizedExpressionSyntax(
    ExpressionSyntax Expression,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record PropertyExpressionSyntax(
    ExpressionSyntax Target,
    string Property,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan)
{
    public bool NullSafe { get; init; }
}

public sealed record IndexExpressionSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax Index,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record ListExpressionSyntax(
    IReadOnlyList<ExpressionSyntax> Items,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record ObjectFieldSyntax(
    string Name,
    ExpressionSyntax Value,
    SourceSpan Span);

public sealed record ObjectExpressionSyntax(
    IReadOnlyList<ObjectFieldSyntax> Fields,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);
