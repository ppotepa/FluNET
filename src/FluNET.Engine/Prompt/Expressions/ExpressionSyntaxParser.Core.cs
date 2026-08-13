namespace FluNET.Prompt.Expressions;

public sealed partial class ExpressionSyntaxParser
{
    private readonly IReadOnlyList<ExpressionToken> _tokens;
    private int _position;

    private ExpressionSyntaxParser(IReadOnlyList<ExpressionToken> tokens)
    {
        _tokens = tokens;
    }
}
