namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private ExpressionSyntax ParseUnary() => ParsePrimary();
}
