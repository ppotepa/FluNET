namespace FluNET.Tokens.Tree;

public class TokenTree
{
    public Token? Root { get; private set; }
    public Token? Last { get; private set; }
    public int Count { get; private set; }


    public TokenTree()
    {
    }

    internal void AddToken(Token token)
    {
        if (Root is null)
        {
            Root = token;
            Last = Root;
        }
        else
        {
            token.Previous = Last;
            if (Last != null)
            {
                Last.Next = token;
            }
            Last = token;
        }

        Count += 1;
    }

    public IEnumerable<Token> GetTokens()
    {
        Token? current = Root;
        while (current != null)
        {
            yield return current;
            current = current.Next;
        }
    }

    public override string ToString()
    {
        return string.Join(" ", GetTokens().Select(t => t.Value));
    }
}
