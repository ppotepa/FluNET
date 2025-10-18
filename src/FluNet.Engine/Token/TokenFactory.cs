namespace FluNET.Tokens
{
    public class TokenFactory
    {
        public TokenFactory()
        {
        }

        internal Token CreateToken(RawToken rawToken)
        {
            return new Token(rawToken.Value, TokenType.Regular);
        }
    }
}