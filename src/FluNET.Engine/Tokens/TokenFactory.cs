namespace FluNET.Tokens
{
    public class TokenFactory
    {
        public TokenFactory()
        {
        }

        internal Token CreateToken(RawToken rawToken)
        {
            string value = rawToken.Value;

            if (value is "." or "?" or "!")
            {
                return new Token(value, TokenType.Terminal);
            }

            if (value.StartsWith('[') && value.EndsWith(']'))
            {
                return new Token(value, TokenType.Variable);
            }

            if (value.StartsWith('{') && value.EndsWith('}'))
            {
                return new Token(value, TokenType.Reference);
            }

            // Default to regular token
            return new Token(value, TokenType.Regular);
        }
    }
}
