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

            // Check for variable pattern [variable]
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                return new Token(value, TokenType.Variable);
            }

            // Check for reference pattern {reference}
            if (value.StartsWith("{") && value.EndsWith("}"))
            {
                return new Token(value, TokenType.Reference);
            }

            // Default to regular token
            return new Token(value, TokenType.Regular);
        }
    }
}